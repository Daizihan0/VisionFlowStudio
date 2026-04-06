using System.IO;
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
chcp 65001 >nul
REM VisionFlow Studio 自动化脚本
REM 生成时间: {generatedAt}
REM 项目文件: {projectFileName}

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
