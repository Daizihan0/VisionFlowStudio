using System.Windows.Media;
using VisionFlowStudio.Core.Models;

namespace VisionFlowStudio.App.ViewModels;

public sealed class LogEntryViewModel : ObservableObject
{
    public LogEntryViewModel(ExecutionLogEntry model)
    {
        TimeText = model.Timestamp.ToString("HH:mm:ss");
        Level = model.Level;
        Message = model.Message;
        NodeId = model.NodeId;
        LevelBrush = new SolidColorBrush(Level switch
        {
            "成功" => (Color)ColorConverter.ConvertFromString("#4ADE80"),
            "警告" => (Color)ColorConverter.ConvertFromString("#FBBF24"),
            "错误" => (Color)ColorConverter.ConvertFromString("#F87171"),
            _ => (Color)ColorConverter.ConvertFromString("#93C5FD")
        });
    }

    public string TimeText { get; }

    public string Level { get; }

    public string Message { get; }

    public Guid? NodeId { get; }

    public Brush LevelBrush { get; }
}
