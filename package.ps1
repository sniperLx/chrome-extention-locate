[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$DistributionDirectory = Join-Path $ProjectRoot "dist"
$PackageDirectory = Join-Path $DistributionDirectory "extension-folder-locator"
$PackagePath = Join-Path $DistributionDirectory "extension-folder-locator.zip"

New-Item -ItemType Directory -Path $DistributionDirectory -Force | Out-Null

if (Test-Path -LiteralPath $PackageDirectory) {
    $resolvedPackageDirectory = [System.IO.Path]::GetFullPath($PackageDirectory)
    $resolvedDistributionDirectory = [System.IO.Path]::GetFullPath($DistributionDirectory)
    if (-not $resolvedPackageDirectory.StartsWith($resolvedDistributionDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "打包目录不在 dist 中，已停止操作。"
    }
    Remove-Item -LiteralPath $resolvedPackageDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $PackagePath) {
    Remove-Item -LiteralPath $PackagePath -Force
}

New-Item -ItemType Directory -Path $PackageDirectory -Force | Out-Null

$items = @(
    "manifest.json",
    "popup.html",
    "popup.css",
    "popup.js",
    "icons",
    "native-host",
    "install.ps1",
    "uninstall.ps1",
    "安装本机助手.cmd",
    "卸载本机助手.cmd",
    "README.md"
)

foreach ($item in $items) {
    $source = Join-Path $ProjectRoot $item
    if (-not (Test-Path -LiteralPath $source)) {
        throw "缺少打包文件：$source"
    }
    Copy-Item -LiteralPath $source -Destination $PackageDirectory -Recurse -Force
}

Compress-Archive -LiteralPath $PackageDirectory -DestinationPath $PackagePath -CompressionLevel Optimal
Write-Host "安装包已生成：$PackagePath" -ForegroundColor Green
