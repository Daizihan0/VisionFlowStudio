namespace VisionFlowStudio.App.Services;

/// <summary>
/// 命令行参数解析结果
/// </summary>
public sealed record CommandLineOptions
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
