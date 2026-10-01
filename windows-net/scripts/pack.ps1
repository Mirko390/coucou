<#
.SYNOPSIS
  Builds the Windows release: the published app, a portable zip and the MSI.

.DESCRIPTION
  Leaves in windows-net\release\ the same file names the release workflow
  publishes:

    Coucou-Windows-X.Y.Z-x64.msi   the versioned installer
    Coucou-Windows-x64.msi         the same file under the rolling name
    Coucou-Windows-X.Y.Z-x64.zip   portable: unzip anywhere, run coucou.exe

  Needs the .NET 10 SDK, Node 20+, and the MSVC build tools (for the AOT relay).

.PARAMETER Runtime
  win-x64 (default) or win-arm64.

.PARAMETER FrameworkDependent
  Ship without the .NET runtime: a much smaller download, but the machine needs
  the .NET 10 Desktop Runtime installed.
#>
param(
  [ValidateSet("win-x64", "win-arm64")] [string] $Runtime = "win-x64",
  [switch] $FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$arch = $Runtime.Substring(4)

$version = ([xml](Get-Content (Join-Path $root "Directory.Build.props"))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in Directory.Build.props" }

$publish = Join-Path $root "artifacts\publish\$Runtime"
$release = Join-Path $root "release"
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
New-Item -ItemType Directory -Force $release | Out-Null

# The ILCompiler finds link.exe through vcvarsall, which calls vswhere by name.
$installer = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer"
if ((Test-Path $installer) -and -not ($env:PATH -split ';' -contains $installer)) { $env:PATH += ";$installer" }

Write-Host "`n  Coucou $version for $Runtime`n"

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }
dotnet publish (Join-Path $root "app\Coucou.csproj") -c Release -r $Runtime --self-contained $selfContained -o $publish -nologo
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
foreach ($required in "coucou.exe", "coucou-hook.exe", "wwwroot\index.html", "wwwroot\settings.html") {
  if (-not (Test-Path (Join-Path $publish $required))) { throw "the published app is missing $required" }
}

dotnet build (Join-Path $root "installer\Coucou.Installer.wixproj") -c Release "-p:PublishDir=$publish\" "-p:InstallerPlatform=$arch" -nologo
if ($LASTEXITCODE -ne 0) { throw "installer build failed" }
$msi = Get-ChildItem (Join-Path $root "installer\bin") -Recurse -Filter "Coucou.msi" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

$versioned = Join-Path $release "Coucou-Windows-$version-$arch.msi"
$rolling = Join-Path $release "Coucou-Windows-$arch.msi"
$zip = Join-Path $release "Coucou-Windows-$version-$arch.zip"
Copy-Item $msi.FullName $versioned -Force
Copy-Item $msi.FullName $rolling -Force
if (Test-Path $zip) { Remove-Item $zip }
Get-ChildItem $publish -Exclude *.pdb | Compress-Archive -DestinationPath $zip

Write-Host ""
foreach ($file in $versioned, $rolling, $zip) {
  "  {0,-40} {1,8:N1} MB" -f (Split-Path -Leaf $file), ((Get-Item $file).Length / 1MB)
}
Write-Host ""
