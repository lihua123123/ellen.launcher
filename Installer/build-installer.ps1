# Build the installer in the ONLY correct order.
#
# Why this script exists: the bundled 753 data (index.json + 753 items + assets/)
# lives under Installer\Publish\Resource\EnemyArchives\Web753 - it is NOT part of
# the dotnet build output. A plain "robocopy /MIR" from bin\Release to Publish
# therefore DELETES it, and the packaged app silently loses the offline 753
# baseline (first run shows an empty "waiting for sync" view instead).
#
# Order: build -> mirror -> regenerate bundle -> (optional) ISCC
# Exit codes: 0 ok; 1 build/mirror failed; 2 bundle missing after sync.

param(
  [switch]$SkipBuild,
  [switch]$SkipInstaller,
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $repo

$proj = "src\Ellen.Launcher\Ellen.Launcher\Ellen.Launcher.csproj"
$outDir = "src\Ellen.Launcher\Ellen.Launcher\bin\$Configuration\net10.0-windows10.0.26100.0\win-x64"
$publish = "Installer\Publish"
$bundle = Join-Path $publish "Resource\EnemyArchives\Web753"

if (-not $SkipBuild) {
  Write-Host "[1/4] dotnet build -c $Configuration -p:Platform=x64"
  & dotnet build $proj -c $Configuration -p:Platform=x64 --nologo
  if ($LASTEXITCODE -ne 0) { Write-Error "build failed ($LASTEXITCODE)"; exit 1 }
}

Write-Host "[2/4] robocopy /MIR $outDir -> $publish"
& robocopy $outDir $publish /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -gt 7) { Write-Error "robocopy failed ($LASTEXITCODE)"; exit 1 }

Write-Host "[3/4] regenerate bundled 753 (json + assets)"
& python tools\EllenWikiSync\sync_bundle.py
if ($LASTEXITCODE -ne 0) { Write-Error "sync_bundle failed ($LASTEXITCODE)"; exit 1 }

# The mirror step above deletes Web753 entirely, so the real-device acceptance
# evidence (gate G3b items 3/4) must be restored from its durable copy before
# sync_assets reads it. Without this every rebuild silently resets it to {}.
$acceptance = Join-Path $repo "..\workspace\wiki-migration\reports\enemy-assets-acceptance.json"
$acceptanceDst = Join-Path $bundle "assets"
if (Test-Path $acceptance) {
  New-Item -ItemType Directory -Force -Path $acceptanceDst | Out-Null
  Copy-Item $acceptance (Join-Path $acceptanceDst "acceptance.json") -Force
  Write-Host "[3/4] restored acceptance evidence from reports/enemy-assets-acceptance.json"
} else {
  Write-Host "[3/4] WARNING: acceptance evidence missing; G3b will stay partial"
}

& python tools\EllenWikiSync\sync_assets.py
if ($LASTEXITCODE -ne 0) { Write-Error "sync_assets failed ($LASTEXITCODE)"; exit 1 }

if (-not (Test-Path (Join-Path $bundle "index.json"))) {
  Write-Error "bundled index.json missing after sync: $bundle"
  exit 2
}
Write-Host "[ok] bundle ready: $bundle"

if (-not $SkipInstaller) {
  Write-Host "[4/4] iscc Installer\installer.iss"
  $iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
  if (-not (Test-Path $iscc)) { $iscc = "iscc" }
  & $iscc "Installer\installer.iss"
  if ($LASTEXITCODE -ne 0) { Write-Error "iscc failed ($LASTEXITCODE)"; exit 1 }
}

Write-Host "done"
