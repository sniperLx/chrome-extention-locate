# 插件目录定位器

一个用于定位 Chrome 已安装扩展文件位置的 Manifest V3 扩展。它可以可视化浏览当前浏览器中的扩展，并按名称或 ID 搜索；选中插件后，一键在 Windows 资源管理器中打开其实际安装目录。

## 功能

- 读取并展示当前 Chrome 中已安装的扩展
- 按名称、ID 搜索，支持仅显示已启用插件
- 识别插件版本、启用状态和图标
- 一键打开插件安装文件夹，并回显完整路径
- 支持 Chrome、Chrome Beta、Chrome Dev、Chrome Canary、Edge、Brave 和 Chromium 的默认用户数据目录
- 支持存在多个浏览器配置文件时自动查找

## 为什么需要本机助手

Chrome 扩展运行在浏览器沙箱中。`chrome.management` API 可以提供插件 ID、名称和状态，但不会暴露插件在磁盘上的安装路径；扩展页面也无权限启动 Windows 资源管理器。

因此本项目由两个部分组成：

1. Chrome 扩展：负责展示、搜索和选择插件。
2. Native Messaging 本机助手：读取浏览器的配置文件，定位插件目录，并调用资源管理器打开。

本机助手只接受格式为 32 位 Chrome 扩展 ID 的请求，不接收任意文件路径，并且 Native Messaging 清单只允许安装时检测到的当前扩展 ID 调用。

## 安装

### 1. 加载扩展

1. 打开 `chrome://extensions`
2. 开启右上角的“开发者模式”
3. 点击“加载已解压的扩展程序”
4. 选择本项目目录

扩展加载后会在工具栏显示。首次打开时，顶部会提示“还需安装本机助手”。

### 2. 安装本机助手

直接双击项目根目录中的 `安装本机助手.cmd`，或在 PowerShell 中运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

安装脚本会：

- 使用 Windows 自带的 .NET Framework 4 编译器构建本机助手
- 自动从浏览器配置中识别刚刚加载的扩展 ID
- 将助手复制到 `%LOCALAPPDATA%\ExtensionFolderLocator`
- 为 current-user 注册 Chrome、Edge、Brave 和 Chromium 的 Native Messaging 清单

脚本无需管理员权限。安装完成后回到扩展，点击右上角的状态按钮重新检测即可。

如果自动识别失败，可以在 `chrome://extensions` 中复制扩展 ID，然后执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -ExtensionId 你的32位扩展ID
```

### 3. 使用

1. 点击工具栏中的扩展图标
2. 搜索并选择目标插件
3. 点击“打开文件夹”

插件文件夹会在 Windows 资源管理器中打开，底部同时显示完整路径，可点击“复制”。

## 目录结构

```text
.
├── manifest.json             # Chrome 扩展清单
├── popup.html/css/js         # 扩展界面和交互
├── icons/                    # 扩展图标
├── native-host/
│   ├── NativeHost.cs         # Native Messaging 本机助手
│   └── build.ps1             # .NET Framework 编译脚本
├── install.ps1               # 安装与注册脚本
├── uninstall.ps1             # 卸载脚本
└── package.ps1               # 生成发布压缩包
```

## 开发与测试

构建本机助手：

```powershell
powershell -ExecutionPolicy Bypass -File .\native-host\build.ps1
```

使用命令行查看所有支持的浏览器根目录：

```powershell
.\native-host\bin\ExtensionFolderLocator.exe --roots
```

定位一个指定扩展：

```powershell
.\native-host\bin\ExtensionFolderLocator.exe --locate 扩展ID
```

生成发布包：

```powershell
powershell -ExecutionPolicy Bypass -File .\package.ps1
```

## 已知限制

- 默认扫描 Windows 常见浏览器的标准用户数据目录。若 Chrome 使用自定义 `--user-data-dir`，需要按相同思路扩展 `NativeHost.cs` 中的根目录发现逻辑。
- 企业策略安装的组件扩展可能没有普通用户可访问的常规安装目录。
- 商店扩展通常位于 `用户数据目录\Profile\Extensions\扩展ID\版本目录`；解压安装的扩展则直接使用开发者选择的目录。
- 本项目以“加载已解压的扩展程序”为主要使用方式。若提交到 Chrome Web Store，需要按商店政策重新评估远程代码和本机助手分发方式。

## 卸载

双击 `卸载本机助手.cmd`，或在 PowerShell 中运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

随后在 `chrome://extensions` 中移除扩展即可。
