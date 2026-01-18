#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${ROOT_DIR}/artifacts/publish"
RUNTIME="${1:-win-x64}"
CONFIGURATION="${2:-Release}"

mkdir -p "${OUTPUT_DIR}"

dotnet publish "${ROOT_DIR}/src/Unfollowed.App/Unfollowed.App.csproj" \
  -c "${CONFIGURATION}" \
  -r "${RUNTIME}" \
  --self-contained true \
  /p:PublishSingleFile=true \
  /p:PublishReadyToRun=true \
  -o "${OUTPUT_DIR}/${RUNTIME}"

echo "Publish complete: ${OUTPUT_DIR}/${RUNTIME}"
