param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$Root = Split-Path -Parent $PSScriptRoot
$PropsPath = Join-Path $Root "Directory.Build.props"
$PropsXml = [xml](Get-Content $PropsPath)
$Version = $PropsXml.Project.PropertyGroup.Version
if (-not $Version) {
  throw "Unable to determine version from Directory.Build.props."
}
$PublishRoot = Join-Path $Root "artifacts/publish/$Runtime"
$Output = Join-Path $PublishRoot $Version
$Dist = Join-Path $Root "artifacts/dist"

New-Item -ItemType Directory -Path $Output -Force | Out-Null
New-Item -ItemType Directory -Path $Dist -Force | Out-Null

dotnet publish "$Root/src/Unfollowed.App/Unfollowed.App.csproj" `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  /p:PublishReadyToRun=true `
  /p:Version=$Version `
  -o $Output

Copy-Item -Path (Join-Path $Root "CHANGELOG.md") -Destination $Output -Force
$ZipName = "Unfollowed-$Version-$Runtime.zip"
$ZipPath = Join-Path $Dist $ZipName
if (Test-Path $ZipPath) {
  Remove-Item $ZipPath -Force
}
Compress-Archive -Path (Join-Path $Output "*") -DestinationPath $ZipPath

Write-Host "Publish complete: $Output"
Write-Host "Distribution package: $ZipPath"
