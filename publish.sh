#!/usr/bin/env bash
# publish.sh — Build and publish OTClient for the current platform.
# Usage: ./publish.sh [win-x64|linux-x64|osx-x64|osx-arm64]
#
# Requires: .NET 10 SDK  (https://dotnet.microsoft.com/download)
# Task 10.9 – Platform publish scripts.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSHARP_DIR="$SCRIPT_DIR/csharp"
SLN="$CSHARP_DIR/OTClient.slnx"
LAUNCHER="$CSHARP_DIR/src/OTClient.Launcher/OTClient.Launcher.csproj"
OUT_BASE="$SCRIPT_DIR/publish"

RID="${1:-}"

# Auto-detect platform when no argument given
if [[ -z "$RID" ]]; then
    OS="$(uname -s)"
    ARCH="$(uname -m)"
    case "$OS-$ARCH" in
        Linux-x86_64)   RID="linux-x64"   ;;
        Linux-aarch64)  RID="linux-arm64"  ;;
        Darwin-x86_64)  RID="osx-x64"     ;;
        Darwin-arm64)   RID="osx-arm64"   ;;
        MINGW*|MSYS*|CYGWIN*)
                        RID="win-x64"     ;;
        *)              echo "Cannot auto-detect RID for '$OS-$ARCH'. Pass it explicitly."; exit 1 ;;
    esac
fi

OUT="$OUT_BASE/$RID"
echo "=== OTClient Publish ==="
echo "  RID        : $RID"
echo "  Output dir : $OUT"
echo ""

dotnet publish "$LAUNCHER" \
    --runtime "$RID" \
    --configuration Release \
    --self-contained true \
    --output "$OUT" \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=true \
    -p:IncludeNativeLibrariesForSelfExtract=true

echo ""
echo "=== Published to $OUT ==="
