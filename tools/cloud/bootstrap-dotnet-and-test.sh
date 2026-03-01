#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

export DOTNET_ROOT
export PATH="${DOTNET_ROOT}:${DOTNET_ROOT}/tools:${PATH}"

has_supported_dotnet() {
  if ! command -v dotnet >/dev/null 2>&1; then
    return 1
  fi

  dotnet --list-sdks | grep -Eq '^(8|9|[1-9][0-9])\.'
}

if ! has_supported_dotnet; then
  echo "Installing .NET SDK (channel 8.0) into ${DOTNET_ROOT}..."
  install_script="${DOTNET_ROOT}/dotnet-install.sh"
  mkdir -p "${DOTNET_ROOT}"
  curl -fsSL "https://dot.net/v1/dotnet-install.sh" -o "${install_script}"
  bash "${install_script}" --channel "8.0" --quality "ga" --install-dir "${DOTNET_ROOT}"
fi

echo "Using dotnet: $(dotnet --version)"
dotnet restore "${REPO_ROOT}/MiniPainterHub.sln"
dotnet build "${REPO_ROOT}/MiniPainterHub.sln" -c Release --no-restore
dotnet test "${REPO_ROOT}/MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj" -c Release --no-build
