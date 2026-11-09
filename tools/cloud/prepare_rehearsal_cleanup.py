"""Generate (never deploy) a disabled, narrowly scoped Azure cleanup workflow.

No Azure SDK, credentials, subprocesses or network access. Review the generated
subscription template, run what-if and prove a scheduled cleanup before spending.
"""
import argparse
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import re
import uuid

SUB = "/subscriptions/23df46c9-3639-4a03-bb4b-61234224142b"
APP_GROUP = SUB + "/resourceGroups/mphp-rg"
APP = APP_GROUP + "/providers/Microsoft.Web/sites/rollandpaint-prod-ca"
PLAN = APP_GROUP + "/providers/Microsoft.Web/serverfarms/ASP-mphprg-linux-stage"
HOST = "https://management.azure.com"
ALL = ["Succeeded", "Failed", "Skipped", "TimedOut"]
SCHEMA = "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"
PROBE_SETTINGS = {"ASPNETCORE_ENVIRONMENT": "Production", "DOTNET_ENVIRONMENT": "Production",
                  "SCM_DO_BUILD_DURING_DEPLOYMENT": "false", "ENABLE_ORYX_BUILD": "false"}


def timestamp(value):
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None or parsed.utcoffset() != timedelta(0):
        raise ValueError("Use an explicit UTC timestamp")
    return parsed


def verify_workflow_definition(actual, expected):
    """Accept only Azure's matching evaluated schedule; reject all other drift."""
    checked = copy.deepcopy(actual)
    for trigger in checked.get("triggers", {}).values():
        if "evaluatedRecurrence" in trigger:
            if trigger["evaluatedRecurrence"] != trigger.get("recurrence"):
                raise ValueError("Azure evaluated schedule differs from configured recurrence")
            del trigger["evaluatedRecurrence"]
    if checked != expected:
        raise ValueError("Deployed workflow definition differs from reviewed template")


def validate(run_id, cleanup_at, deadline, allowed_ids, now, maximum_hours=2):
    if not re.fullmatch(r"\d{8}-[a-z0-9]{6}", run_id):
        raise ValueError("Run ID must be YYYYMMDD- followed by six lowercase letters/digits")
    cleanup, end = timestamp(cleanup_at), timestamp(deadline)
    if maximum_hours not in (2, 4):
        raise ValueError("Only two-hour rehearsal or four-hour approved migration windows are supported")
    if not now < cleanup < end <= now + timedelta(hours=maximum_hours):
        raise ValueError("Prepared window must be future and within the approved duration")
    if end - cleanup < timedelta(minutes=30):
        raise ValueError("Reserve at least 30 minutes for cleanup")
    group = SUB + "/resourceGroups/rg-mph-linux-rehearsal-" + run_id
    if len(allowed_ids) > 100 or len(set(x.lower() for x in allowed_ids)) != len(allowed_ids):
        raise ValueError("Resource allowlist must be unique and bounded")
    for target in allowed_ids:
        if not target.lower().startswith(group.lower() + "/providers/") or any(x in target for x in ("?", "#", "..", "@", "[", "'")):
            raise ValueError("Resource allowlist escaped the new disposable group")
    return group


def http(method, resource, api="2023-12-01", body=None, after=()):
    inputs = {"method": method, "uri": HOST + resource + "?api-version=" + api,
              "authentication": {"type": "ManagedServiceIdentity", "audience": HOST + "/"},
              "retryPolicy": {"type": "exponential", "count": 2, "interval": "PT5S", "maximumInterval": "PT15S"}}
    if body is not None:
        inputs["body"] = body
        inputs["headers"] = {"Content-Type": "application/json"}
    return {"type": "Http", "inputs": inputs, "operationOptions": "DisableAsyncPattern",
            "runAfter": {name: ALL for name in after}}


def condition(expression, actions, after=()):
    return {"type": "If", "expression": expression, "actions": actions, "else": {"actions": {}},
            "runAfter": {name: ALL for name in after}}


def definition(run_id, group, workflow, cleanup_at, allowed_ids):
    # Logic Apps requires literal UTC Z (no offset or fractional seconds), even
    # though ARM validation accepts ISO offsets. Round up so the first tick is
    # not fractionally earlier than the deadline predicate and ownership tags.
    schedule = timestamp(cleanup_at)
    if schedule.microsecond:
        schedule += timedelta(seconds=1)
    schedule_text = schedule.strftime("%Y-%m-%dT%H:%M:%SZ")
    # These three scopes are siblings, not dependent on successful prior cleanup.
    candidate_actions = {"Read_candidate": http("GET", APP)}
    edits = {}
    operations = [
        ("Stop_candidate", "POST", APP + "/stop", None),
        ("Scrub_settings", "PUT", APP + "/config/appsettings", {"properties": PROBE_SETTINGS}),
        ("Scrub_connections", "PUT", APP + "/config/connectionstrings", {"properties": {}}),
        ("Remove_identity", "PATCH", APP, {"identity": {"type": "None"}}),
        ("Disable_always_on", "PATCH", APP + "/config/web", {"properties": {"alwaysOn": False,
            "appCommandLine": "dotnet LinuxCapacityProbe.dll"}}),
        ("Stop_again", "POST", APP + "/stop", None),
    ]
    previous = ()
    for name, method, target, body in operations:
        edits[name] = http(method, target, body=body, after=previous)
        previous = (name,)
    candidate_actions["Candidate_owned"] = condition(
        "@equals(toLower(coalesce(body('Read_candidate')?['properties']?['serverFarmId'], '')), '" + PLAN.lower() + "')",
        edits, ("Read_candidate",))
    plan_actions = {"Plan_members": http("GET", PLAN + "/sites")}
    plan_actions["Candidate_members"] = {"type": "Query", "inputs": {
        "from": "@coalesce(body('Plan_members')?['value'], json('[]'))",
        "where": "@equals(toLower(item()?['id']), '" + APP.lower() + "')"},
        "runAfter": {"Plan_members": ["Succeeded"]}}
    plan_actions["Plan_is_isolated"] = condition(
        "@and(equals(outputs('Plan_members')?['statusCode'], 200), "
        "equals(length(coalesce(body('Plan_members')?['value'], json('[]'))), 1), "
        "equals(length(body('Candidate_members')), 1))",
        {"Return_F1": http("PATCH", PLAN, body={"sku": {"name": "F1", "tier": "Free", "capacity": 1}})}, ("Candidate_members",))
    group_actions = {"Read_group": http("GET", group, "2022-09-01")}
    owned = {"Inventory": http("GET", group + "/resources", "2021-04-01")}
    owned["Unexpected_resources"] = {"type": "Query", "inputs": {
        "from": "@coalesce(body('Inventory')?['value'], json('[]'))",
        "where": "@not(contains(parameters('allowedResourceIds'), toLower(item()?['id'])))"},
        "runAfter": {"Inventory": ["Succeeded"]}}
    owned["Inventory_matches"] = condition(
        "@and(equals(outputs('Inventory')?['statusCode'], 200), "
        "empty(body('Inventory')?['nextLink']), equals(length(body('Unexpected_resources')), 0))",
        {"Delete_group": http("DELETE", group, "2022-09-01")}, ("Unexpected_resources",))
    group_actions["Group_owned"] = condition(
        "@and(equals(outputs('Read_group')?['statusCode'], 200), "
        "equals(body('Read_group')?['tags']?['purpose'], 'mph-linux-core-rehearsal'), "
        "equals(body('Read_group')?['tags']?['runId'], '" + run_id + "'), "
        "equals(body('Read_group')?['tags']?['cleanupAtUtc'], '" + cleanup_at + "'))",
        owned, ("Read_group",))
    due = {name: {"type": "Scope", "actions": actions, "runAfter": {}} for name, actions in
           [("Candidate", candidate_actions), ("Plan", plan_actions), ("Dependencies", group_actions)]}
    for name, target, api in [("Verify_candidate", APP, "2023-12-01"),
                              ("Verify_plan", PLAN, "2023-12-01"),
                              ("Verify_group_absent", group, "2022-09-01")]:
        due[name] = http("GET", target, api, after=("Candidate", "Plan", "Dependencies"))
    succeeded = ", ".join("equals(actions('" + name + "')?['status'], 'Succeeded')" for name, *_ in operations)
    verified = ("@and(" + succeeded + ", equals(actions('Return_F1')?['status'], 'Succeeded'), "
                "equals(body('Verify_candidate')?['properties']?['state'], 'Stopped'), "
                "equals(coalesce(body('Verify_candidate')?['identity']?['type'], 'None'), 'None'), "
                "equals(body('Verify_plan')?['sku']?['name'], 'F1'), "
                "equals(outputs('Verify_group_absent')?['statusCode'], 404))")
    due["Cleanup_verified"] = condition(verified, {
        "Disable_finished_workflow": http("POST", workflow + "/disable", "2019-05-01")
    }, ("Verify_candidate", "Verify_plan", "Verify_group_absent"))
    return {"$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
            "contentVersion": "1.0.0.0", "parameters": {"allowedResourceIds": {"type": "Array", "defaultValue": [x.lower() for x in allowed_ids]}},
            "triggers": {"Deadline": {"type": "Recurrence", "recurrence": {"frequency": "Minute", "interval": 2,
                "startTime": schedule_text}, "runtimeConfiguration": {"concurrency": {"runs": 1}}}},
            "actions": {"Due": condition("@greaterOrEquals(ticks(utcNow()), ticks('" + cleanup_at + "'))", due)},
            "outputs": {}}


def template(run_id, cleanup_at, deadline, allowed_ids, *, now=None, arm_after_permissions=False, maximum_hours=2):
    group = validate(run_id, cleanup_at, deadline, allowed_ids, now or datetime.now(timezone.utc), maximum_hours)
    cleanup_group = SUB + "/resourceGroups/rg-mph-linux-cleanup-" + run_id
    workflow = cleanup_group + "/providers/Microsoft.Logic/workflows/mph-deadline-" + run_id
    tags = {"purpose": "mph-linux-core-rehearsal", "runId": run_id, "cleanupAtUtc": cleanup_at, "deadlineUtc": deadline}
    resources = [{"type": "Microsoft.Resources/resourceGroups", "apiVersion": "2022-09-01",
                  "name": target.split("/")[-1], "location": "canadacentral", "tags": tags}
                 for target in (group, cleanup_group)]
    roles = {}
    permissions = {
        "candidate": (["Microsoft.Web/sites/read", "Microsoft.Web/sites/stop/action", "Microsoft.Web/sites/write",
                       "Microsoft.Web/sites/config/write"], APP_GROUP),
        "plan": (["Microsoft.Web/serverfarms/read", "Microsoft.Web/serverfarms/sites/read", "Microsoft.Web/serverfarms/write"], APP_GROUP),
        "delete": (["Microsoft.Resources/subscriptions/resourceGroups/delete", "Microsoft.Resources/subscriptions/resourceGroups/resources/read"], group),
        # RG-level assignments vanish with the deleted RG. This metadata-only read
        # permission survives so 404 can be established; no resources or secrets read.
        "observe": (["Microsoft.Resources/subscriptions/resourceGroups/read"], SUB),
        "self": (["Microsoft.Logic/workflows/disable/action"], cleanup_group),
    }
    for key, (actions, assignable) in permissions.items():
        name = str(uuid.uuid5(uuid.NAMESPACE_URL, workflow + "/role/" + key))
        roles[key] = SUB + "/providers/Microsoft.Authorization/roleDefinitions/" + name
        resources.append({"type": "Microsoft.Authorization/roleDefinitions", "apiVersion": "2022-04-01", "name": name,
            "dependsOn": [group, cleanup_group], "properties": {"roleName": "mph-cleanup-" + run_id + "-" + key,
            "type": "CustomRole", "description": "Bounded rehearsal cleanup: " + key,
            "assignableScopes": [assignable], "permissions": [{"actions": actions, "notActions": [], "dataActions": [], "notDataActions": []}]}})

    def assignment(scope, role, principal):
        name = str(uuid.uuid5(uuid.NAMESPACE_URL, workflow + "/assignment/" + scope + "/" + role))
        result = {"type": "Microsoft.Authorization/roleAssignments", "apiVersion": "2022-04-01", "name": name,
                  "properties": {"roleDefinitionId": roles[role], "principalId": principal, "principalType": "ServicePrincipal"}}
        # Subscription/RG grants inherit the containing deployment's scope.
        # An absolute subscription/RG string in resource.scope is misinterpreted
        # as an extension resource namespace by the actual deployment engine.
        if "/providers/" in scope:
            result["scope"] = scope
        return result

    principal = "[reference('" + workflow + "', '2019-05-01', 'Full').identity.principalId]"
    definition_value = definition(run_id, group, workflow, cleanup_at, allowed_ids)
    workflow_resource = {"type": "Microsoft.Logic/workflows", "apiVersion": "2019-05-01", "name": workflow.split("/")[-1],
        "location": "canadacentral", "identity": {"type": "SystemAssigned"}, "tags": tags,
        "properties": {"state": "Disabled", "definition": definition_value, "parameters": {}}}
    self_assignment = assignment(workflow, "self", principal)
    self_assignment["dependsOn"] = [workflow]
    deployment_name = "mph-cleanup-workflow-" + run_id
    resources.append({"type": "Microsoft.Resources/deployments", "apiVersion": "2022-09-01", "name": deployment_name,
        "resourceGroup": cleanup_group.split("/")[-1], "dependsOn": [cleanup_group, *roles.values()],
        "properties": {"mode": "Incremental", "expressionEvaluationOptions": {"scope": "inner"}, "template": {
            "$schema": SCHEMA, "contentVersion": "1.0.0.0", "resources": [workflow_resource, self_assignment],
            "outputs": {"principalId": {"type": "string", "value": principal}}}}})
    deployment_id = cleanup_group + "/providers/Microsoft.Resources/deployments/" + deployment_name
    principal_output = "[reference('" + deployment_id + "', '2022-09-01').outputs.principalId.value]"
    # The metadata observer is the only subscription-level grant; all mutations
    # are assigned at the exact candidate, plan, disposable RG or workflow.
    observer = assignment(SUB, "observe", principal_output)
    observer["dependsOn"] = [deployment_id]
    resources.append(observer)
    grant_deployments = []
    for name, rg, pairs in [("candidate", "mphp-rg", [(APP, "candidate"), (PLAN, "plan")]),
                            ("dependencies", group.split("/")[-1], [(group, "delete")])]:
        grant_deployments.append(SUB + "/resourceGroups/" + rg + "/providers/Microsoft.Resources/deployments/mph-cleanup-" + name + "-" + run_id)
        nested = [assignment(scope, role, "[parameters('principalId')]") for scope, role in pairs]
        resources.append({"type": "Microsoft.Resources/deployments", "apiVersion": "2022-09-01",
            "name": "mph-cleanup-" + name + "-" + run_id, "resourceGroup": rg, "dependsOn": [deployment_id],
            "properties": {"mode": "Incremental", "expressionEvaluationOptions": {"scope": "inner"},
                "parameters": {"principalId": {"value": principal_output}}, "template": {
                    "$schema": SCHEMA, "contentVersion": "1.0.0.0", "parameters": {"principalId": {"type": "string"}},
                    "resources": nested}}})
    if arm_after_permissions:
        # The second write is performed by ARM only after every grant exists.
        # There is no desktop-dependent enable step. Defaults remain disabled.
        enabled_workflow = copy.deepcopy(workflow_resource)
        enabled_workflow["properties"]["state"] = "Enabled"
        resources.append({"type": "Microsoft.Resources/deployments", "apiVersion": "2022-09-01",
            "name": "mph-cleanup-arm-" + run_id, "resourceGroup": cleanup_group.split("/")[-1],
            "dependsOn": [deployment_id, SUB + "/providers/Microsoft.Authorization/roleAssignments/" + observer["name"], *grant_deployments],
            "properties": {"mode": "Incremental", "expressionEvaluationOptions": {"scope": "inner"},
                "template": {"$schema": SCHEMA, "contentVersion": "1.0.0.0", "resources": [enabled_workflow]}}})
    return {"$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
            "contentVersion": "1.0.0.0", "metadata": {"preparedOnly": True, "runId": run_id, "armAfterPermissions": arm_after_permissions,
                "cleanupAtUtc": cleanup_at, "deadlineUtc": deadline,
                "warning": "Not spending approval or proof. Review what-if. " + ("ARM enables after all grants complete." if arm_after_permissions else "Workflow stays disabled.")},
            "resources": resources, "outputs": {"workflowId": {"type": "string", "value": workflow},
                "temporaryGroupId": {"type": "string", "value": group}}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--cleanup-at", required=True)
    parser.add_argument("--deadline", required=True)
    parser.add_argument("--allowed-resource-ids", type=Path, help="JSON array from reviewed new-group what-if; empty for cleanup proof")
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--arm-after-permissions", action="store_true", help="Explicit opt-in: ARM enables the timer after all grants complete; requires a fresh approved proof window")
    args = parser.parse_args()
    allowed = json.loads(args.allowed_resource_ids.read_text(encoding="utf-8")) if args.allowed_resource_ids else []
    result = template(args.run_id, args.cleanup_at, args.deadline, allowed, arm_after_permissions=args.arm_after_permissions)
    if args.output.exists():
        raise ValueError("Refusing to overwrite a prepared template")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    data = (json.dumps(result, indent=2) + "\n").encode("utf-8")
    args.output.write_bytes(data)
    print(json.dumps({"preparedOnly": True, "sha256": hashlib.sha256(data).hexdigest(), "output": str(args.output)}))


if __name__ == "__main__":
    main()
