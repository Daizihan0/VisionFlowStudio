# 导出启动脚本功能实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用户可以将已保存的项目导出为 .bat 脚本，双击脚本即可静默执行项目，完成后通过 Windows Toast 通知结果。

**Architecture:** 修改 App.xaml.cs 添加 CLI 参数解析，支持 `--execute`、`--silent`、`--notify` 参数；静默模式下直接调用执行引擎而不显示主窗口；新增 ToastNotificationService 处理通知；MainViewModel 新增 ExportScriptCommand 生成 .bat 脚本。

**Tech Stack:** .NET 8, WPF, Windows Toast Notifications (via Microsoft.Toolkit.Uwp.Notifications 或简化方案)

---

## File Structure

| 文件 | 操作 | 职责 |
|------|------|------|
| `VisionFlowStudio.App/Services/CommandLineOptions.cs` | 创建 | CLI 参数模型 |
| `VisionFlowStudio.App/Services/ToastNotificationService.cs` | 创建 | Windows Toast 通知服务 |
| `VisionFlowStudio.App/Services/ScriptExporter.cs` | 创建 | .bat 脚本生成器 |
| `VisionFlowStudio.App/App.xaml` | 修改 | 移除 StartupUri，改为代码控制启动 |
| `VisionFlowStudio.App/App.xaml.cs` | 修改 | CLI 参数解析、静默执行入口 |
| `VisionFlowStudio.App/ViewModels/MainViewModel.cs` | 修改 | 添加 ExportScriptCommand |
| `VisionFlowStudio.App/MainWindow.xaml` | 修改 | 工具栏添加「导出脚本」按钮 |

---

### Task 1: 创建 CommandLineOptions 模型

**Files:**
- Create: `VisionFlowStudio.App/Services/CommandLineOptions.cs`

- [ ] **Step 1: 创建 CommandLineOptions.cs**

```csharp
namespace VisionFlowStudio.App.Services;

/// <summary>
/// 命令行参数解析结果
/// </summary>
public sealed class CommandLineOptions
{
    /// <summary>
    /// 是否为执行模式（--execute 参数）
    /// </summary>
    public bool IsExecuteMode { get; init; }

    /// <summary>
    /// 要执行的项目文件路径
    /// </summary>
    public string? ProjectFilePath { get; init; }

    /// <summary>
    /// 是否静默模式（--silent 参数）
    /// </summary>
    public bool IsSilent { get; init; }

    /// <summary>
    /// 是否显示通知（--notify 参数）
    /// </summary>
    public bool ShowNotification { get; init; }

    /// <summary>
    /// 是否显示帮助（--help 参数）
    /// </summary>
    public bool ShowHelp { get; init; }

    /// <summary>
    /// 解析命令行参数
    /// </summary>
    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions
        {
            IsExecuteMode = false,
            ProjectFilePath = null,
            IsSilent = false,
            ShowNotification = false,
            ShowHelp = false
        };

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--execute":
                case "-e":
                    options = options with { IsExecuteMode = true };
                    if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                    {
                        options = options with { ProjectFilePath = args[i + 1] };
                        i++;
                    }
                    break;
                case "--silent":
                case "-s":
                    options = options with { IsSilent = true };
                    break;
                case "--notify":
                case "-n":
                    options = options with { ShowNotification = true };
                    break;
                case "--help":
                case "-h":
                case "/?":
                    options = options with { ShowHelp = true };
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// 获取帮助文本
    /// </summary>
    public static string GetHelpText()
    {
        return """
            VisionFlow Studio - 视觉流程自动化工具

            用法:
              VisionFlowStudio.App.exe [选项]

            选项:
              --execute, -e <路径>   执行指定的项目文件后退出
              --silent, -s           静默模式：隐藏主窗口
              --notify, -n           执行完毕后显示 Windows 通知
              --help, -h             显示此帮助信息

            示例:
              VisionFlowStudio.App.exe                                    # 正常启动
              VisionFlowStudio.App.exe --execute project.vfs.json        # 执行项目并显示窗口
              VisionFlowStudio.App.exe --execute project.vfs.json -s -n  # 静默执行并通知
            """;
    }
}
```

- [ ] **Step 2: 验证编译通过**

Run: `dotnet build VisionFlowStudio.App/VisionFlowStudio.App.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add VisionFlowStudio.App/Services/CommandLineOptions.cs
git commit -m "feat: add CommandLineOptions for CLI argument parsing"
```

---

### Task 2: 创建 ToastNotificationService

**Files:**
- Create: `VisionFlowStudio.App/Services/ToastNotificationService.cs`

- [ ] **Step 1: 创建 ToastNotificationService.cs**

使用简化的通知方案（System.Windows.Forms.NotifyIcon），避免引入额外 NuGet 包：

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace VisionFlowStudio.App.Services;

/// <summary>
/// Windows 桌面通知服务
/// </summary>
public sealed class ToastNotificationService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _disposed;

    /// <summary>
    /// 显示成功通知
    /// </summary>
    public void ShowSuccess(string title, string message)
    {
        ShowNotification(title, message, ToolTipIcon.Info);
    }

    /// <summary>
    /// 显示错误通知
    /// </summary>
    public void ShowError(string title, string message)
    {
        ShowNotification(title, message, ToolTipIcon.Error);
    }

    /// <summary>
    /// 显示警告通知
    /// </summary>
    public void ShowWarning(string title, string message)
    {
        ShowNotification(title, message, ToolTipIcon.Warning);
    }

    private void ShowNotification(string title, string message, ToolTipIcon icon)
    {
        EnsureNotifyIcon();

        _notifyIcon!.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.Visible = true;
        _notifyIcon.ShowBalloonTip(5000);
    }

    private void EnsureNotifyIcon()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "VisionFlow Studio",
            Visible = false
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
    }
}
```

- [ ] **Step 2: 添加 System.Drawing.Common 和 System.Windows.Forms 引用（如果需要）**

检查 .csproj 是否已有 System.Drawing.Common（已有）。需要添加 System.Windows.Forms：

修改 `VisionFlowStudio.App/VisionFlowStudio.App.csproj`，在 `<ItemGroup>` 中添加：

```xml
<PackageReference Include="System.Windows.Forms" Version="8.0.0" />
```

完整 ItemGroup 应为：

```xml
<ItemGroup>
    <ProjectReference Include="..\VisionFlowStudio.Core\VisionFlowStudio.Core.csproj" />
    <ProjectReference Include="..\VisionFlowStudio.Infrastructure\VisionFlowStudio.Infrastructure.csproj" />
    <PackageReference Include="OpenCvSharp4" Version="4.10.0.20240616" />
    <PackageReference Include="OpenCvSharp4.Extensions" Version="4.10.0.20240616" />
    <PackageReference Include="OpenCvSharp4.runtime.win" Version="4.10.0.20240616" />
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
    <PackageReference Include="System.Windows.Forms" Version="8.0.0" />
</ItemGroup>
```

- [ ] **Step 3: 验证编译通过**

Run: `dotnet build VisionFlowStudio.App/VisionFlowStudio.App.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add VisionFlowStudio.App/Services/ToastNotificationService.cs VisionFlowStudio.App/VisionFlowStudio.App.csproj
git commit -m "feat: add ToastNotificationService for desktop notifications"
```

---

### Task 3: 创建 ScriptExporter 服务

**Files:**
- Create: `VisionFlowStudio.App/Services/ScriptExporter.cs`

- [ ] **Step 1: 创建 ScriptExporter.cs**

```csharp
using System.Reflection;

namespace VisionFlowStudio.App.Services;

/// <summary>
/// 批处理脚本导出器
/// </summary>
public static class ScriptExporter
{
    /// <summary>
    /// 生成 .bat 脚本内容
    /// </summary>
    /// <param name="projectFileName">项目文件名（如 demo.vfs.json）</param>
    /// <returns>脚本内容</returns>
    public static string GenerateBatScript(string projectFileName)
    {
        var appPath = Assembly.GetExecutingAssembly().Location;
        var generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        return $@"@echo off
:: VisionFlow Studio 自动化脚本
:: 生成时间: {generatedAt}
:: 项目文件: {projectFileName}

set ""APP_PATH={appPath}""
set ""PROJECT_PATH=%~dp0{projectFileName}""

if not exist ""%PROJECT_PATH%"" (
    echo 错误：项目文件不存在
    echo 路径：%PROJECT_PATH%
    pause
    exit /b 1
)

""%APP_PATH%"" --execute ""%PROJECT_PATH%"" --silent --notify
";
    }

    /// <summary>
    /// 获取默认导出文件名
    /// </summary>
    /// <param name="projectName">项目名称</param>
    /// <returns>建议的 .bat 文件名</returns>
    public static string GetSuggestedFileName(string projectName)
    {
        var safeName = string.Join("_", projectName.Split(Path.GetInvalidFileNameChars()));
        return string.IsNullOrWhiteSpace(safeName) ? "Automation.bat" : $"{safeName}.bat";
    }
}
```

- [ ] **Step 2: 验证编译通过**

Run: `dotnet build VisionFlowStudio.App/VisionFlowStudio.App.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add VisionFlowStudio.App/Services/ScriptExporter.cs
git commit -m "feat: add ScriptExporter for generating .bat launcher scripts"
```

---

### Task 4: 修改 App.xaml 和 App.xaml.cs 支持 CLI 执行

**Files:**
- Modify: `VisionFlowStudio.App/App.xaml`
- Modify: `VisionFlowStudio.App/App.xaml.cs`

- [ ] **Step 1: 修改 App.xaml，移除 StartupUri**

将 `StartupUri="MainWindow.xaml"` 移除，改为代码控制启动：

```xml
<Application x:Class="VisionFlowStudio.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:VisionFlowStudio.App"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>

    </Application.Resources>
</Application>
```

注意：添加了 `ShutdownMode="OnExplicitShutdown"` 以便静默模式下控制应用退出。

- [ ] **Step 2: 修改 App.xaml.cs，添加 CLI 处理逻辑**

```csharp
using System.Windows;
using VisionFlowStudio.App.Services;
using VisionFlowStudio.Core.Services;
using VisionFlowStudio.Infrastructure.Services;

namespace VisionFlowStudio.App;

/// <summary>
/// Interaction logic for App
/// </summary>
public partial class App : Application
{
    private MainWindow? _mainWindow;
    private ToastNotificationService? _toastNotificationService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = CommandLineOptions.Parse(e.Args);

        if (options.ShowHelp)
        {
            Console.WriteLine(CommandLineOptions.GetHelpText());
            Shutdown(0);
            return;
        }

        if (options.IsExecuteMode && !string.IsNullOrWhiteSpace(options.ProjectFilePath))
        {
            await ExecuteProjectAsync(options);
            return;
        }

        // 正常启动模式
        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _mainWindow.Show();
    }

    private async Task ExecuteProjectAsync(CommandLineOptions options)
    {
        _toastNotificationService = new ToastNotificationService();

        try
        {
            var projectFilePath = options.ProjectFilePath!;

            // 解析相对路径
            if (!Path.IsPathRooted(projectFilePath))
            {
                projectFilePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, projectFilePath));
            }

            // 检查文件是否存在
            if (!File.Exists(projectFilePath))
            {
                var errorMsg = $"项目文件不存在：{projectFilePath}";
                if (options.ShowNotification)
                {
                    _toastNotificationService.ShowError("VisionFlow Studio", errorMsg);
                }
                else
                {
                    Console.Error.WriteLine(errorMsg);
                }
                Shutdown(1);
                return;
            }

            // 加载项目
            var storageService = new JsonProjectStorageService();
            var project = await storageService.LoadAsync(projectFilePath, CancellationToken.None);

            // 创建执行引擎
            var visionMatcherService = new VisionMatcherService();
            var inputSimulationService = new InputSimulationService();
            var executionEngine = new DesktopFlowExecutionEngine(visionMatcherService, inputSimulationService);

            // 执行日志收集
            var logs = new List<string>();
            var success = true;

            try
            {
                await executionEngine.ExecutePreviewAsync(
                    project,
                    entry => logs.Add($"[{entry.Level}] {entry.Message}"),
                    (_, _) => { },
                    CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                success = false;
                logs.Add("[警告] 执行被取消");
            }
            catch (Exception ex)
            {
                success = false;
                logs.Add($"[错误] 执行失败：{ex.Message}");
            }

            if (options.ShowNotification)
            {
                if (success)
                {
                    _toastNotificationService.ShowSuccess("VisionFlow Studio", $"项目「{project.Name}」执行完成");
                }
                else
                {
                    _toastNotificationService.ShowError("VisionFlow Studio", $"项目「{project.Name}」执行失败");
                }
            }
            else
            {
                foreach (var log in logs)
                {
                    Console.WriteLine(log);
                }
            }

            // 给通知一点时间显示
            if (options.ShowNotification)
            {
                await Task.Delay(1000);
            }

            Shutdown(success ? 0 : 1);
        }
        catch (Exception ex)
        {
            if (options.ShowNotification)
            {
                _toastNotificationService.ShowError("VisionFlow Studio", $"执行出错：{ex.Message}");
                await Task.Delay(1000);
            }
            else
            {
                Console.Error.WriteLine($"错误：{ex.Message}");
            }
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _toastNotificationService?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 3: 添加必要的 using 语句**

确保 App.xaml.cs 顶部有所有必要的 using：

```csharp
using System.IO;
using System.Windows;
using VisionFlowStudio.App.Services;
using VisionFlowStudio.Core.Services;
using VisionFlowStudio.Infrastructure.Services;
```

- [ ] **Step 4: 验证编译通过**

Run: `dotnet build VisionFlowStudio.App/VisionFlowStudio.App.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add VisionFlowStudio.App/App.xaml VisionFlowStudio.App/App.xaml.cs
git commit -m "feat: add CLI execution mode with --execute, --silent, --notify options"
```

---

### Task 5: 在 MainViewModel 添加 ExportScriptCommand

**Files:**
- Modify: `VisionFlowStudio.App/ViewModels/MainViewModel.cs`

- [ ] **Step 1: 添加 ExportScriptCommand 字段和属性**

在 MainViewModel 类中，找到其他 Command 声明的位置（约第 130 行附近），添加：

```csharp
public ICommand ExportScriptCommand { get; }
```

同时在私有字段区域（约第 20-30 行）添加：

```csharp
private readonly RelayCommand _exportScriptCommand;
```

- [ ] **Step 2: 在构造函数中初始化命令**

在构造函数中其他命令初始化的位置添加：

```csharp
_exportScriptCommand = new RelayCommand(async _ => await ExportScriptAsync(), _ => CanExportScript());
ExportScriptCommand = _exportScriptCommand;
```

- [ ] **Step 3: 实现 CanExportScript 和 ExportScriptAsync 方法**

在 MainViewModel 类的末尾（其他私有方法附近）添加：

```csharp
private bool CanExportScript()
{
    return !string.IsNullOrWhiteSpace(_currentFilePath) && !IsBusy;
}

private async Task ExportScriptAsync()
{
    if (string.IsNullOrWhiteSpace(_currentFilePath))
    {
        Log("警告", "请先保存项目后再导出脚本。");
        return;
    }

    try
    {
        var projectFileName = Path.GetFileName(_currentFilePath);
        var suggestedScriptName = ScriptExporter.GetSuggestedFileName(ProjectName);

        var dialog = new SaveFileDialog
        {
            Filter = "批处理脚本 (*.bat)|*.bat",
            FileName = suggestedScriptName,
            InitialDirectory = Path.GetDirectoryName(_currentFilePath)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var scriptContent = ScriptExporter.GenerateBatScript(projectFileName);
        await File.WriteAllTextAsync(dialog.FileName, scriptContent, System.Text.Encoding.UTF8);

        Log("成功", $"脚本已导出到：{dialog.FileName}");
    }
    catch (Exception ex)
    {
        Log("错误", $"导出脚本失败：{ex.Message}");
    }
}
```

- [ ] **Step 4: 添加 using 语句**

在 MainViewModel.cs 顶部添加：

```csharp
using VisionFlowStudio.App.Services;
```

- [ ] **Step 5: 在 RaiseCommandStates 方法中添加命令刷新**

找到 `RaiseCommandStates` 方法（约第 1460 行），添加：

```csharp
_exportScriptCommand.RaiseCanExecuteChanged();
```

- [ ] **Step 6: 验证编译通过**

Run: `dotnet build VisionFlowStudio.App/VisionFlowStudio.App.csproj`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add VisionFlowStudio.App/ViewModels/MainViewModel.cs
git commit -m "feat: add ExportScriptCommand to MainViewModel"
```

---

### Task 6: 在 MainWindow.xaml 添加「导出脚本」按钮

**Files:**
- Modify: `VisionFlowStudio.App/MainWindow.xaml`

- [ ] **Step 1: 找到工具栏按钮区域，添加导出脚本按钮**

在 MainWindow.xaml 中找到「保存工程」按钮附近（搜索 `SaveProjectCommand`），在其后添加导出脚本按钮：

找到类似以下结构的位置（约第 260-280 行）：

```xml
<Button Style="{StaticResource ToolbarButtonStyle}" Command="{Binding SaveProjectCommand}" Content="保存工程" />
```

在其后添加：

```xml
<Button Style="{StaticResource ToolbarButtonStyle}" Command="{Binding ExportScriptCommand}" Content="导出脚本" />
```

- [ ] **Step 2: 验证编译通过**

Run: `dotnet build VisionFlowStudio.App/VisionFlowStudio.App.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add VisionFlowStudio.App/MainWindow.xaml
git commit -m "feat: add Export Script button to toolbar"
```

---

### Task 7: 集成测试与修复

- [ ] **Step 1: 完整编译**

Run: `dotnet build VisionFlowStudio.sln -c Release`
Expected: Build succeeded with no errors

- [ ] **Step 2: 手动测试清单**

1. 正常启动应用：`dotnet run --project VisionFlowStudio.App`
2. 创建一个简单流程并保存为 `.vfs.json` 文件
3. 点击「导出脚本」按钮，检查是否弹出保存对话框
4. 保存脚本后，检查脚本内容是否正确
5. 在命令行运行生成的 `.bat` 文件，验证静默执行和通知功能
6. 测试各种 CLI 参数组合：
   - `--execute project.vfs.json`
   - `--execute project.vfs.json --silent`
   - `--execute project.vfs.json --silent --notify`
   - `--help`

- [ ] **Step 3: 修复发现的问题**

如果测试中发现问题，立即修复并提交。

---

### Task 8: 最终提交

- [ ] **Step 1: 确认所有改动已提交**

Run: `git status`
Expected: nothing to commit, working tree clean

- [ ] **Step 2: 创建功能分支或标签（可选）**

```bash
git checkout -b feature/export-execution-script
# 或
git tag v1.1.0-export-script
```
