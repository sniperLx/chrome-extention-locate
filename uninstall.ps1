[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$HostName = "com.extension_folder_locator.host"
$InstallDirectory = Join-Path $env:LOCALAPPDATA "ExtensionFolderLocator"
$RegistryBases = @(
    "Software\Google\Chrome\NativeMessagingHosts",
    "Software\Microsoft\Edge\NativeMessagingHosts",
    "Software\BraveSoftware\Brave-Browser\NativeMessagingHosts",
    "Software\Chromium\NativeMessagingHosts"
)

foreach ($registryBase in $RegistryBases) {
    $container = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey(
        $registryBase,
        $true
    )
    if ($container) {
        try {
            if ($container.GetSubKeyNames() -contains $HostName) {
                $container.DeleteSubKeyTree($HostName, $false)
                Write-Host "已移除注册表项：HKCU\$registryBase\$HostName"
            }
        } finally {
            $container.Dispose()
        }
    }
}

if (Test-Path -LiteralPath $InstallDirectory) {
    $resolvedInstallDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
    $expectedParent = [System.IO.Path]::GetFullPath($env:LOCALAPPDATA)
    if (-not $resolvedInstallDirectory.StartsWith($expectedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "安装目录不在预期位置，已停止删除：$resolvedInstallDirectory"
    }

    Remove-Item -LiteralPath $resolvedInstallDirectory -Recurse -Force
    Write-Host "已移除本机助手目录：$resolvedInstallDirectory"
}

Write-Host "卸载完成。" -ForegroundColor Green
