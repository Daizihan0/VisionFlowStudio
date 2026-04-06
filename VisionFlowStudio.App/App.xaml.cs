using System.IO;
using System.Windows;
using VisionFlowStudio.App.Services;
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
            if (!options.IsSilent)
            {
                ShowMainWindow();  // Show window for non-silent execution
            }
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
            var screenCaptureService = new ScreenCaptureService();
            var visionMatcherService = new VisionMatcherService(screenCaptureService);
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
