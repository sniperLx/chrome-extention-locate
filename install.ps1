[CmdletBinding()]
param(
    [ValidatePattern("^[a-pA-P]{32}$")]
    [string]$ExtensionId
)

$ErrorActionPreference = "Stop"

$HostName = "com.extension_folder_locator.host"
$ExtensionName = "插件目录定位器"
$ProjectRoot = $PSScriptRoot
$HostSource = Join-Path $ProjectRoot "native-host\bin\ExtensionFolderLocator.exe"
$BuildScript = Join-Path $ProjectRoot "native-host\build.ps1"
$InstallDirectory = Join-Path $env:LOCALAPPDATA "ExtensionFolderLocator"
$InstalledHost = Join-Path $InstallDirectory "ExtensionFolderLocator.exe"
$ManifestPath = Join-Path $InstallDirectory "$HostName.json"

function Find-LoadedExtensionId {
    $localAppData = [Environment]::GetFolderPath("LocalApplicationData")
    $profileRoots = @(
        (Join-Path $localAppData "Google\Chrome\User Data"),
        (Join-Path $localAppData "Google\Chrome Beta\User Data"),
        (Join-Path $localAppData "Google\Chrome Dev\User Data"),
        (Join-Path $localAppData "Google\Chrome SxS\User Data"),
        (Join-Path $localAppData "Microsoft\Edge\User Data"),
        (Join-Path $localAppData "BraveSoftware\Brave-Browser\User Data"),
        (Join-Path $localAppData "Chromium\User Data")
    )

    foreach ($profileRoot in $profileRoots) {
        if (-not (Test-Path -LiteralPath $profileRoot)) {
            continue
        }

        foreach ($profileDirectory in (Get-ChildItem -LiteralPath $profileRoot -Directory -ErrorAction SilentlyContinue)) {
            foreach ($preferenceName in @("Secure Preferences", "Preferences")) {
                $preferencePath = Join-Path $profileDirectory.FullName $preferenceName
                if (-not (Test-Path -LiteralPath $preferencePath)) {
                    continue
                }

                try {
                    $preferences = Get-Content -LiteralPath $preferencePath -Raw -Encoding UTF8 | ConvertFrom-Json
                    $settings = $preferences.extensions.settings
                    if (-not $settings) {
                        continue
                    }

                    foreach ($property in $settings.PSObject.Properties) {
                        $setting = $property.Value
                        $manifestName = [string]$setting.manifest.name
                        $settingPath = [string]$setting.path
                        $matchesPath = $false

                        if ($settingPath) {
                            try {
                                $resolvedPath = [System.IO.Path]::GetFullPath(
                                    [Environment]::ExpandEnvironmentVariables($settingPath)
                                )
                                $matchesPath = $resolvedPath.TrimEnd("\") -ieq $ProjectRoot.TrimEnd("\")
                            } catch {
                                $matchesPath = $false
                            }
                        }

                        if ($matchesPath -or $manifestName -eq $ExtensionName) {
                            return $property.Name
                        }
                    }
                } catch {
                    # A running browser may be rewriting this file; other profiles are still checked.
                }
            }
        }
    }

    return $null
}

if (-not (Test-Path -LiteralPath $BuildScript)) {
    throw "未找到编译脚本：$BuildScript"
}

Write-Host "正在编译本机助手..." -ForegroundColor Cyan
& $BuildScript

if (-not $ExtensionId) {
    Write-Host "正在查找已加载的“$ExtensionName”扩展..." -ForegroundColor Cyan
    $ExtensionId = Find-LoadedExtensionId
}

if (-not $ExtensionId) {
    throw @"
未找到已加载的扩展。

请先在 Chrome 打开 chrome://extensions：
1. 开启右上角“开发者模式”
2. 点击“加载已解压的扩展程序”
3. 选择当前项目目录：$ProjectRoot
4. 重新运行本安装脚本

也可以使用 -ExtensionId 参数手动指定扩展 ID。
"@
}

if ($ExtensionId -notmatch "^[a-pA-P]{32}$") {
    throw "扩展 ID 格式不正确：$ExtensionId"
}

$ExtensionId = $ExtensionId.ToLowerInvariant()

if (-not (Test-Path -LiteralPath $HostSource)) {
    throw "未找到本机助手程序：$HostSource"
}

New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
Copy-Item -LiteralPath $HostSource -Destination $InstalledHost -Force

$manifest = [ordered]@{
    name = $HostName
    description = "Locate installed Chrome extension folders"
    path = $InstalledHost
    type = "stdio"
    allowed_origins = @("chrome-extension://$ExtensionId/")
}

$manifestJson = $manifest | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText(
    $ManifestPath,
    $manifestJson,
    (New-Object System.Text.UTF8Encoding($false))
)

$registryBases = @(
    "Software\Google\Chrome\NativeMessagingHosts",
    "Software\Microsoft\Edge\NativeMessagingHosts",
    "Software\BraveSoftware\Brave-Browser\NativeMessagingHosts",
    "Software\Chromium\NativeMessagingHosts"
)

foreach ($registryBase in $registryBases) {
    $registryKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey(
        "$registryBase\$HostName",
        [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree
    )
    if (-not $registryKey) {
        throw "无法创建注册表项：HKCU\$registryBase\$HostName"
    }
    try {
        $registryKey.SetValue("", $ManifestPath, [Microsoft.Win32.RegistryValueKind]::String)
    } finally {
        $registryKey.Dispose()
    }
}

Write-Host ""
Write-Host "安装完成。" -ForegroundColor Green
Write-Host "扩展 ID：$ExtensionId"
Write-Host "本机助手：$InstalledHost"
Write-Host "Native Messaging 清单：$ManifestPath"
Write-Host ""
Write-Host "回到扩展窗口，点击右上角的“助手未连接”即可重新检测。" -ForegroundColor Cyan
