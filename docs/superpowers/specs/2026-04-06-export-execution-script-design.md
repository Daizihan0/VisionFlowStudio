# 导出启动脚本功能设计

## 概述

用户可以将已保存的自动化项目导出为 .bat 命令行启动脚本，双击脚本即可静默执行项目，无需打开主程序界面。执行完毕后通过 Windows Toast 通知显示结果。

## 需求

- **导出形式**：.bat 命令行启动器
- **执行方式**：静默运行 + Windows 通知结果
- **保存位置**：用户通过保存对话框自选
- **项目路径**：相对路径（脚本与项目一起移动仍可用）
- **主程序路径**：导出时自动检测并嵌入绝对路径

## 命令行参数设计

### 主程序 CLI 参数

```
VisionFlowStudio.exe [选项]

选项：
  --execute <项目文件路径>    执行指定的项目文件后退出
  --silent                    静默模式：隐藏主窗口
  --notify                    执行完毕后显示 Windows Toast 通知
  --help                      显示帮助信息
```

### 使用示例

```bash
# 静默执行并通知
VisionFlowStudio.exe --execute "..\projects\demo.vfs.json" --silent --notify

# 正常打开（无参数）
VisionFlowStudio.exe
```

### 行为规则

| 参数组合 | 行为 |
|---------|------|
| 无参数 | 正常启动，显示主窗口 |
| `--execute` | 执行项目，显示主窗口和执行进度 |
| `--execute --silent` | 执行项目，隐藏主窗口 |
| `--execute --silent --notify` | 静默执行，完成后 Toast 通知结果 |
| `--execute` + 项目文件不存在 | Toast 通知错误，退出码 1 |

## 导出脚本功能设计

### UI 入口

- 主窗口工具栏添加「导出脚本」按钮

### 导出流程

1. 用户点击「导出脚本」
2. 检查当前项目是否已保存（有 `_currentFilePath`）
3. 若未保存，提示用户先保存项目
4. 弹出保存对话框，默认文件名与当前项目同名（如 `demo.bat`）
5. 用户选择保存位置后，生成 .bat 文件

### 生成的 .bat 脚本模板

```batch
@echo off
:: VisionFlow Studio 自动化脚本
:: 生成时间: 2026-04-06 15:30:00

set "APP_PATH=C:\Program Files\VisionFlowStudio\VisionFlowStudio.App.exe"
set "PROJECT_PATH=%~dp0demo.vfs.json"

if not exist "%PROJECT_PATH%" (
    echo 错误：项目文件不存在
    pause
    exit /b 1
)

"%APP_PATH%" --execute "%PROJECT_PATH%" --silent --notify
```

### 脚本逻辑说明

- `%~dp0` 获取脚本所在目录，实现相对路径
- 导出时检查项目文件名，脚本与项目文件名对应
- 主程序路径 `APP_PATH` 在导出时嵌入（检测当前运行的 exe 路径）

## 技术实现

### 1. 命令行参数解析

修改 `App.xaml.cs`：
- 在 `OnStartup` 中解析命令行参数
- 检测 `--execute`、`--silent`、`--notify` 参数
- 若有 `--execute`，走静默执行路径

### 2. 静默执行模式

修改 `App.xaml.cs` 和 `MainWindow.xaml.cs`：
- `--silent` 模式下不显示主窗口
- 直接调用 `DesktopFlowExecutionEngine` 执行项目
- 执行完毕后退出应用

### 3. Windows Toast 通知

新增 `ToastNotificationService`：
- 使用 `Windows.UI.Notifications` 或简化方案（`System.Windows.Forms.NotifyIcon` + Balloon）
- 通知内容：成功/失败 + 简要信息

### 4. 导出脚本功能

在 `MainViewModel` 中添加 `ExportScriptCommand`：
- 检查项目是否已保存
- 打开保存对话框让用户选择 .bat 保存位置
- 生成脚本内容并写入文件

## 文件变更

| 文件 | 变更 |
|------|------|
| `App.xaml.cs` | 添加命令行参数解析、静默执行路径 |
| `App.xaml` | 添加 ShutdownMode 设置 |
| `MainViewModel.cs` | 添加 ExportScriptCommand |
| `MainWindow.xaml` | 工具栏添加「导出脚本」按钮 |
| `Services/ToastNotificationService.cs` | 新增，Toast 通知服务 |
| `Services/CommandLineOptions.cs` | 新增，CLI 参数模型 |
