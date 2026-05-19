[CmdletBinding()]
param(
    [string]$SubscriptionId = "23df46c9-3639-4a03-bb4b-61234224142b",
    [string]$TenantId = "4b4b6ba8-5186-4d09-b9b2-8c95f729c4b2",
    [string]$ResourceGroupName = "rg-minipainterhub-prod",
    [string]$Location = "eastus",
    [string]$IdentityName = "id-gha-minipainterhub-prod",
    [string]$GitHubOwner = "glebovski1",
    [string]$GitHubRepo = "MiniPainterHub",
    [string]$GitHubEnvironment = "production",
    [string]$GitHubReviewer = "glebovski1",
    [string]$SqlAdminLogin = "mphsqladmin",
    [switch]$ConfigureGitHub,
    [switch]$SkipProviderRegistration
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Invoke-External {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $Command $($Arguments -join ' ')"
    }
}

function Invoke-ExternalJson {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    $output = & $Command @Arguments --output json
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $Command $($Arguments -join ' ')"
    }

    if ([string]::IsNullOrWhiteSpace($output)) {
        return $null
    }

    return $output | ConvertFrom-Json
}

function New-SqlPassword {
    return "Mph!" + [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")
}

function New-JwtKey {
    $bytes = New-Object byte[] 64
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        if ($null -ne $rng) {
            $rng.Dispose()
        }
    }

    return [Convert]::ToBase64String($bytes)
}

Assert-Command "az"

if ($ConfigureGitHub) {
    Assert-Command "gh"
}

Write-Host "Checking Azure CLI authentication..."
$account = & az account show --output json 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Azure CLI is not freshly authenticated. Run: az logout; az login --tenant `"$TenantId`" --scope `"https://management.core.windows.net//.default`""
}

$accountJson = $account | ConvertFrom-Json
if ($accountJson.tenantId -ne $TenantId) {
    Write-Host "Switching Azure tenant/subscription context..."
}

Invoke-External "az" @("account", "set", "--subscription", $SubscriptionId)

if (-not $SkipProviderRegistration) {
    foreach ($namespace in @("Microsoft.Web", "Microsoft.Sql", "Microsoft.Storage", "Microsoft.ManagedIdentity")) {
        Write-Host "Registering provider $namespace..."
        Invoke-External "az" @("provider", "register", "--namespace", $namespace)
    }
}

Write-Host "Ensuring resource group $ResourceGroupName exists..."
Invoke-External "az" @("group", "create", "--name", $ResourceGroupName, "--location", $Location, "--output", "none")

Write-Host "Ensuring user-assigned identity $IdentityName exists..."
$identityOutput = & az identity show --name $IdentityName --resource-group $ResourceGroupName --output json 2>$null
if ($LASTEXITCODE -eq 0) {
    $identity = $identityOutput | ConvertFrom-Json
}
else {
    $identity = Invoke-ExternalJson "az" @(
        "identity", "create",
        "--name", $IdentityName,
        "--resource-group", $ResourceGroupName,
        "--location", $Location
    )
}

$clientId = $identity.clientId
$principalId = $identity.principalId
$scope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName"

Write-Host "Ensuring Contributor role assignment on $scope..."
$roleAssignment = & az role assignment list --assignee $principalId --scope $scope --role Contributor --query "[0].id" --output tsv
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect role assignments for identity $IdentityName."
}

if ([string]::IsNullOrWhiteSpace($roleAssignment)) {
    Invoke-External "az" @(
        "role", "assignment", "create",
        "--assignee-object-id", $principalId,
        "--assignee-principal-type", "ServicePrincipal",
        "--role", "Contributor",
        "--scope", $scope,
        "--output", "none"
    )
}

$credentialName = "github-$GitHubEnvironment"
$subject = "repo:$GitHubOwner/$GitHubRepo`:environment:$GitHubEnvironment"

Write-Host "Ensuring federated credential $credentialName with subject $subject..."
$credentialOutput = & az identity federated-credential show --identity-name $IdentityName --resource-group $ResourceGroupName --name $credentialName --output json 2>$null
if ($LASTEXITCODE -ne 0) {
    Invoke-External "az" @(
        "identity", "federated-credential", "create",
        "--identity-name", $IdentityName,
        "--resource-group", $ResourceGroupName,
        "--name", $credentialName,
        "--issuer", "https://token.actions.githubusercontent.com",
        "--subject", $subject,
        "--audiences", "api://AzureADTokenExchange",
        "--output", "none"
    )
}

Write-Host ""
Write-Host "Azure OIDC bootstrap is ready."
Write-Host "AZURE_CLIENT_ID=$clientId"
Write-Host "AZURE_TENANT_ID=$TenantId"
Write-Host "AZURE_SUBSCRIPTION_ID=$SubscriptionId"
Write-Host "AZURE_RESOURCE_GROUP=$ResourceGroupName"
Write-Host "AZURE_LOCATION=$Location"
Write-Host "SQL_ADMIN_LOGIN=$SqlAdminLogin"

if ($ConfigureGitHub) {
    $repo = "$GitHubOwner/$GitHubRepo"
    $sqlAdminPassword = New-SqlPassword
    $jwtKey = New-JwtKey

    Write-Host "Configuring GitHub environment $GitHubEnvironment in $repo..."

    $reviewerId = gh api "users/$GitHubReviewer" --jq ".id"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($reviewerId)) {
        throw "Could not resolve GitHub reviewer '$GitHubReviewer'."
    }

    $environmentBody = @{
        wait_timer = 0
        reviewers = @(
            @{
                type = "User"
                id = [int]$reviewerId
            }
        )
    } | ConvertTo-Json -Depth 5

    $environmentBody | gh api --method PUT "repos/$repo/environments/$GitHubEnvironment" --input -
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create or update GitHub environment '$GitHubEnvironment'."
    }

    gh variable set AZURE_CLIENT_ID --repo $repo --env $GitHubEnvironment --body $clientId
    gh variable set AZURE_TENANT_ID --repo $repo --env $GitHubEnvironment --body $TenantId
    gh variable set AZURE_SUBSCRIPTION_ID --repo $repo --env $GitHubEnvironment --body $SubscriptionId
    gh variable set AZURE_RESOURCE_GROUP --repo $repo --env $GitHubEnvironment --body $ResourceGroupName
    gh variable set AZURE_LOCATION --repo $repo --env $GitHubEnvironment --body $Location
    gh variable set SQL_ADMIN_LOGIN --repo $repo --env $GitHubEnvironment --body $SqlAdminLogin

    $sqlAdminPassword | gh secret set AZURE_SQL_ADMIN_PASSWORD --repo $repo --env $GitHubEnvironment
    $jwtKey | gh secret set JWT_KEY --repo $repo --env $GitHubEnvironment

    Write-Host "GitHub variables and required secrets were configured. Store the generated secret values from GitHub if your recovery process requires escrow."
}
else {
    Write-Host ""
    Write-Host "Set these GitHub environment variables on '$GitHubEnvironment':"
    Write-Host "gh variable set AZURE_CLIENT_ID --repo $GitHubOwner/$GitHubRepo --env $GitHubEnvironment --body $clientId"
    Write-Host "gh variable set AZURE_TENANT_ID --repo $GitHubOwner/$GitHubRepo --env $GitHubEnvironment --body $TenantId"
    Write-Host "gh variable set AZURE_SUBSCRIPTION_ID --repo $GitHubOwner/$GitHubRepo --env $GitHubEnvironment --body $SubscriptionId"
    Write-Host "gh variable set AZURE_RESOURCE_GROUP --repo $GitHubOwner/$GitHubRepo --env $GitHubEnvironment --body $ResourceGroupName"
    Write-Host "gh variable set AZURE_LOCATION --repo $GitHubOwner/$GitHubRepo --env $GitHubEnvironment --body $Location"
    Write-Host "gh variable set SQL_ADMIN_LOGIN --repo $GitHubOwner/$GitHubRepo --env $GitHubEnvironment --body $SqlAdminLogin"
    Write-Host ""
    Write-Host "Set these GitHub environment secrets on '$GitHubEnvironment':"
    Write-Host "gh secret set AZURE_SQL_ADMIN_PASSWORD --repo $GitHubOwner/$GitHubRepo --env $GitHubEnvironment"
    Write-Host "gh secret set JWT_KEY --repo $GitHubOwner/$GitHubRepo --env $GitHubEnvironment"
    Write-Host ""
    Write-Host "Create or update the GitHub environment '$GitHubEnvironment' and add required reviewer '$GitHubReviewer'."
}
