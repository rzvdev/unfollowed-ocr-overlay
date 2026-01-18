#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${ROOT_DIR}/artifacts/publish"
DIST_DIR="${ROOT_DIR}/artifacts/dist"
RUNTIME="${1:-win-x64}"
CONFIGURATION="${2:-Release}"

VERSION="$(rg -n --no-heading "<Version>" "${ROOT_DIR}/Directory.Build.props" | sed -E 's/.*<Version>([^<]+)<.*/\\1/')"
if [[ -z "${VERSION}" ]]; then
  echo "Unable to determine version from Directory.Build.props" >&2
  exit 1
fi

mkdir -p "${OUTPUT_DIR}/${RUNTIME}/${VERSION}"
mkdir -p "${DIST_DIR}"

dotnet publish "${ROOT_DIR}/src/Unfollowed.App/Unfollowed.App.csproj" \
  -c "${CONFIGURATION}" \
  -r "${RUNTIME}" \
  --self-contained true \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:EnableCompressionInSingleFile=true \
  /p:PublishReadyToRun=true \
  /p:Version="${VERSION}" \
  -o "${OUTPUT_DIR}/${RUNTIME}/${VERSION}"

cp "${ROOT_DIR}/CHANGELOG.md" "${OUTPUT_DIR}/${RUNTIME}/${VERSION}/CHANGELOG.md"

ZIP_NAME="Unfollowed-${VERSION}-${RUNTIME}.zip"
ZIP_PATH="${DIST_DIR}/${ZIP_NAME}"
rm -f "${ZIP_PATH}"
(cd "${OUTPUT_DIR}/${RUNTIME}/${VERSION}" && zip -r "${ZIP_PATH}" .)

echo "Publish complete: ${OUTPUT_DIR}/${RUNTIME}/${VERSION}"
echo "Distribution package: ${ZIP_PATH}"
