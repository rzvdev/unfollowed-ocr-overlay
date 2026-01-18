param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$Root = Split-Path -Parent $PSScriptRoot
$Output = Join-Path $Root "artifacts/publish/$Runtime"

New-Item -ItemType Directory -Path $Output -Force | Out-Null

dotnet publish "$Root/src/Unfollowed.App/Unfollowed.App.csproj" `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:PublishReadyToRun=true `
  -o $Output

Write-Host "Publish complete: $Output"
