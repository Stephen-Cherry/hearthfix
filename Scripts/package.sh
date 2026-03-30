#!/usr/bin/env bash
set -euo pipefail

VERSION=$(grep -oP '(?<=<Version>)[^<]+' HearthFix.csproj)
ZIP="HearthFix-${VERSION}.zip"

echo "Building HearthFix v${VERSION}..."
dotnet build HearthFix.csproj -c Release

echo "Packaging..."
STAGING=$(mktemp -d)
jq --arg v "$VERSION" '.version_number = $v' manifest.json > "$STAGING/manifest.json"
cp icon.png "$STAGING/"
cp README.md "$STAGING/"
cp "bin/Release/net48/Narolith.HearthFix.dll" "$STAGING/"

(cd "$STAGING" && zip -r "$OLDPWD/$ZIP" .)
rm -rf "$STAGING"

echo "Done: $ZIP"
