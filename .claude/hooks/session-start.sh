#!/bin/bash
# SessionStart hook for Claude Code on the web.
# Installs the toolchain this repo needs so Claude can build, test, run and screenshot the game in the cloud:
#   - .NET 8 SDK          (sim library, xUnit tests, MatchRunner, Godot C# build)
#   - Godot 4.x .NET      (headless import, smoke test, screenshots)
#   - Xvfb + Mesa (GL + Vulkan software drivers) for screenshots with Forward+ or Compatibility
# Idempotent: every step is skipped when already present. Local machines are left untouched.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

GODOT_VERSION="4.5.1"
GODOT_DIR="/opt/Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64"
GODOT_BIN="${GODOT_DIR}/Godot_v${GODOT_VERSION}-stable_mono_linux.x86_64"
GODOT_URL="https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-stable/Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64.zip"
PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"

SUDO=""
if [ "$(id -u)" -ne 0 ]; then
  SUDO="sudo"
fi

log() { echo "[session-start] $*" >&2; }

# 1) System packages
missing=()
command -v dotnet >/dev/null 2>&1 || missing+=(dotnet-sdk-8.0)
command -v xvfb-run >/dev/null 2>&1 || missing+=(xvfb xauth)
command -v unzip >/dev/null 2>&1 || missing+=(unzip)
dpkg -s libgl1-mesa-dri >/dev/null 2>&1 || missing+=(libgl1-mesa-dri)
dpkg -s mesa-vulkan-drivers >/dev/null 2>&1 || missing+=(mesa-vulkan-drivers)
if [ ${#missing[@]} -gt 0 ]; then
  log "installing: ${missing[*]}"
  export DEBIAN_FRONTEND=noninteractive
  $SUDO apt-get update -qq >/dev/null
  $SUDO apt-get install -y -qq "${missing[@]}" >/dev/null
fi

# 2) Godot .NET editor/runtime
if [ ! -x "$GODOT_BIN" ]; then
  log "downloading Godot ${GODOT_VERSION} .NET"
  tmp_zip="$(mktemp --suffix=.zip)"
  curl -fsSL --retry 4 --retry-delay 2 -o "$tmp_zip" "$GODOT_URL"
  $SUDO unzip -q -o "$tmp_zip" -d /opt/
  rm -f "$tmp_zip"
fi

if [ ! -x /usr/local/bin/godot ] || ! grep -q "$GODOT_BIN" /usr/local/bin/godot 2>/dev/null; then
  # Wrapper (not a symlink) so Godot finds its GodotSharp folder next to the real binary.
  printf '#!/bin/bash\nexec "%s" "$@"\n' "$GODOT_BIN" | $SUDO tee /usr/local/bin/godot >/dev/null
  $SUDO chmod +x /usr/local/bin/godot
fi

# 3) Session environment
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
  } >> "$CLAUDE_ENV_FILE"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# 4) Restore NuGet packages and build once (cached in the container snapshot), then import the Godot project.
cd "$PROJECT_DIR"
if [ -f OCT7.sln ]; then
  log "restoring and building OCT7.sln"
  dotnet build OCT7.sln -v q >/dev/null
fi

if [ -f game/project.godot ]; then
  log "importing Godot project"
  timeout 300 godot --headless --path game --import >/dev/null 2>&1 || log "godot import returned non-zero (continuing)"
fi

log "ready: dotnet $(dotnet --version), godot $(godot --version 2>/dev/null | head -n1)"
