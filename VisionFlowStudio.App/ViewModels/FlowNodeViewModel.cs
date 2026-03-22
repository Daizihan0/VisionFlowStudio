using System.Windows;
using System.Windows.Media;
using VisionFlowStudio.Core.Models;

namespace VisionFlowStudio.App.ViewModels;

public sealed class FlowNodeViewModel : ObservableObject
{
    private const double DefaultWidth = 188;
    private const double DefaultHeight = 104;

    private readonly FlowNode _model;
    private bool _isSelected;
    private NodeStatus _status;
    private Brush _surfaceBrush = Brushes.Transparent;
    private Brush _borderBrush = Brushes.Transparent;
    private Brush _accentBrush = Brushes.Transparent;
    private Thickness _borderThicknessValue = new(1);
    private string _settingsEditorText = string.Empty;

    public FlowNodeViewModel(FlowNode model)
    {
        _model = model;
        _settingsEditorText = BuildSettingsEditorText();
        UpdatePalette();
    }

    public Guid Id => _model.Id;

    public FlowNodeKind Kind => _model.Kind;

    public FlowNode Model => _model;

    public double Width => DefaultWidth;

    public double Height => DefaultHeight;

    public double X
    {
        get => _model.Position.X;
        set
        {
            if (Math.Abs(_model.Position.X - value) < 0.01)
            {
                return;
            }

            _model.Position.X = value;
            OnPropertyChanged();
            NotifyGeometryChanged();
        }
    }

    public double Y
    {
        get => _model.Position.Y;
        set
        {
            if (Math.Abs(_model.Position.Y - value) < 0.01)
            {
                return;
            }

            _model.Position.Y = value;
            OnPropertyChanged();
            NotifyGeometryChanged();
        }
    }

    public double InputX => X;

    public double InputY => Y + (Height / 2);

    public double OutputX => X + Width;

    public double OutputY => Y + (Height / 2);

    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);

    public string Title
    {
        get => _model.Title;
        set
        {
            if (_model.Title == value)
            {
                return;
            }

            _model.Title = value;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _model.Description;
        set
        {
            if (_model.Description == value)
            {
                return;
            }

            _model.Description = value;
            OnPropertyChanged();
        }
    }

    public int TimeoutMs
    {
        get => _model.TimeoutMs;
        set
        {
            if (_model.TimeoutMs == value)
            {
                return;
            }

            _model.TimeoutMs = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public int RetryLimit
    {
        get => _model.RetryLimit;
        set
        {
            if (_model.RetryLimit == value)
            {
                return;
            }

            _model.RetryLimit = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            UpdatePalette();
        }
    }

    public NodeStatus Status
    {
        get => _status;
        set
        {
            if (!SetProperty(ref _status, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StatusLabel));
            UpdatePalette();
        }
    }

    public string DisplayKind =>
        Kind switch
        {
            FlowNodeKind.Start => "开始节点",
            FlowNodeKind.Action => "动作节点",
            FlowNodeKind.Vision => "视觉节点",
            FlowNodeKind.Wait => "等待节点",
            FlowNodeKind.Condition => "条件节点",
            FlowNodeKind.SubFlow => "录制片段",
            FlowNodeKind.End => "结束节点",
            _ => "未知节点"
        };

    public string StatusLabel =>
        Status switch
        {
            NodeStatus.Running => "运行中",
            NodeStatus.Succeeded => "完成",
            NodeStatus.Failed => "失败",
            _ => "待机"
        };

    public Brush SurfaceBrush
    {
        get => _surfaceBrush;
        private set => SetProperty(ref _surfaceBrush, value);
    }

    public Brush BorderBrush
    {
        get => _borderBrush;
        private set => SetProperty(ref _borderBrush, value);
    }

    public Brush AccentBrush
    {
        get => _accentBrush;
        private set => SetProperty(ref _accentBrush, value);
    }

    public Thickness BorderThicknessValue
    {
        get => _borderThicknessValue;
        private set => SetProperty(ref _borderThicknessValue, value);
    }

    public string SettingsEditorText
    {
        get => _settingsEditorText;
        set
        {
            if (!SetProperty(ref _settingsEditorText, value))
            {
                return;
            }

            ApplySettingsText(value);
            OnPropertyChanged(nameof(SettingsSummary));
        }
    }

    public string SettingsSummary =>
        _model.Settings.Count == 0
            ? "未配置"
            : string.Join(" · ", _model.Settings.Select(pair => $"{pair.Key}:{pair.Value}"));

    public string AssetSummary =>
        Kind == FlowNodeKind.SubFlow
            ? (_model.RecordedEvents.Count == 0 ? "无录制片段" : $"已录制 {_model.RecordedEvents.Count} 个事件")
            : string.IsNullOrWhiteSpace(_model.AssetPayloadBase64)
                ? "无截图资源"
                : (_model.AssetFileName ?? "内嵌截图资源");

    public void RefreshFromModel()
    {
        _settingsEditorText = BuildSettingsEditorText();
        OnPropertyChanged(nameof(SettingsEditorText));
        OnPropertyChanged(nameof(SettingsSummary));
        OnPropertyChanged(nameof(TimeoutMs));
        OnPropertyChanged(nameof(RetryLimit));
        OnPropertyChanged(nameof(AssetSummary));
    }

    private string BuildSettingsEditorText() =>
        _model.Settings.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, _model.Settings.Select(pair => $"{pair.Key}={pair.Value}"));

    private void ApplySettingsText(string value)
    {
        _model.Settings.Clear();

        var lines = value
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var settingValue = line[(separatorIndex + 1)..].Trim();

            if (key.Length == 0)
            {
                continue;
            }

            _model.Settings[key] = settingValue;
        }
    }

    private void NotifyGeometryChanged()
    {
        OnPropertyChanged(nameof(InputX));
        OnPropertyChanged(nameof(InputY));
        OnPropertyChanged(nameof(OutputX));
        OnPropertyChanged(nameof(OutputY));
        OnPropertyChanged(nameof(CenterX));
        OnPropertyChanged(nameof(CenterY));
    }

    private void UpdatePalette()
    {
        var (surfaceColor, accentColor) = Kind switch
        {
            FlowNodeKind.Start => (ColorFromHex("#122C1D"), ColorFromHex("#4ADE80")),
            FlowNodeKind.Action => (ColorFromHex("#122033"), ColorFromHex("#60A5FA")),
            FlowNodeKind.Vision => (ColorFromHex("#1A2242"), ColorFromHex("#8B5CF6")),
            FlowNodeKind.Wait => (ColorFromHex("#31220D"), ColorFromHex("#F59E0B")),
            FlowNodeKind.Condition => (ColorFromHex("#28153A"), ColorFromHex("#C084FC")),
            FlowNodeKind.SubFlow => (ColorFromHex("#0F2C34"), ColorFromHex("#22D3EE")),
            FlowNodeKind.End => (ColorFromHex("#34141B"), ColorFromHex("#FB7185")),
            _ => (ColorFromHex("#172033"), ColorFromHex("#94A3B8"))
        };

        var borderColor = Status switch
        {
            NodeStatus.Running => ColorFromHex("#FBBF24"),
            NodeStatus.Succeeded => ColorFromHex("#34D399"),
            NodeStatus.Failed => ColorFromHex("#F87171"),
            _ => IsSelected ? ColorFromHex("#E2E8F0") : ColorFromHex("#334155")
        };

        SurfaceBrush = new SolidColorBrush(surfaceColor);
        AccentBrush = new SolidColorBrush(accentColor);
        BorderBrush = new SolidColorBrush(borderColor);
        BorderThicknessValue = IsSelected || Status != NodeStatus.Idle ? new Thickness(2) : new Thickness(1);
    }

    private static Color ColorFromHex(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex);
}
