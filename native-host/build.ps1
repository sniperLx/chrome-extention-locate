[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$frameworkCsc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$frameworkCsc32 = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
$compiler = if (Test-Path $frameworkCsc) { $frameworkCsc } elseif (Test-Path $frameworkCsc32) { $frameworkCsc32 } else { $null }

if (-not $compiler) {
    throw "未找到 .NET Framework 4 C# 编译器。请安装 Windows 自带的 .NET Framework 4.x。"
}

$source = Join-Path $PSScriptRoot "NativeHost.cs"
$outputDirectory = Join-Path $PSScriptRoot "bin"
$output = Join-Path $outputDirectory "ExtensionFolderLocator.exe"

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$compilerArguments = @(
    "/nologo",
    "/target:exe",
    "/optimize+",
    "/codepage:65001",
    "/out:$output",
    "/reference:System.Web.Extensions.dll",
    $source
)

& $compiler $compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "C# 编译器返回错误代码 $LASTEXITCODE"
}

if (-not (Test-Path $output)) {
    throw "编译完成，但未找到输出文件：$output"
}

Write-Host "本机助手编译成功：$output" -ForegroundColor Green
& $output --version
