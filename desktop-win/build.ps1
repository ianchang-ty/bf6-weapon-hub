$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$DistDir = Join-Path $Root "dist"
New-Item -ItemType Directory -Force $DistDir | Out-Null

$NetDir = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$Csc = Join-Path $NetDir "csc.exe"
$WpfDir = Join-Path $NetDir "WPF"
$OutExe = Join-Path $DistDir "BF6WeaponHub.exe"
$BundleGz = Join-Path $Root "data\desktop_bundle.json.gz"
$Manifest = Join-Path $PSScriptRoot "app.manifest"

& $Csc /nologo /optimize+ /target:winexe `
    /out:$OutExe `
    /win32manifest:$Manifest `
    /resource:"$BundleGz,BF6Bundle.gz" `
    /r:"$WpfDir\PresentationFramework.dll" `
    /r:"$WpfDir\PresentationCore.dll" `
    /r:"$WpfDir\WindowsBase.dll" `
    /r:"System.Xaml.dll" `
    /r:"System.Web.Extensions.dll" `
    (Join-Path $PSScriptRoot "GunsmithEngine.cs") `
    (Join-Path $PSScriptRoot "MainWindow.cs") `
    (Join-Path $PSScriptRoot "Program.cs")

if ($LASTEXITCODE -ne 0) { throw "csc compilation failed with exit code $LASTEXITCODE" }

Write-Host "Built: BF6WeaponHub.exe ($((Get-Item $OutExe).Length) bytes)"
$p = Start-Process -FilePath $OutExe -ArgumentList "--self-test" -Wait -PassThru -NoNewWindow
if ($p.ExitCode -ne 0) { throw "Self-test failed with exit code $($p.ExitCode)" }
Write-Host "Self-test passed (ExitCode = 0)!"
