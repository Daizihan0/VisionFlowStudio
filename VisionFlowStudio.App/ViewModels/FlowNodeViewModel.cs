using System.Windows;
using System.Windows.Media;
using VisionFlowStudio.Core.Models;

namespace VisionFlowStudio.App.ViewModels;

public sealed class FlowNodeViewModel : ObservableObject
{
    private const double DefaultWidth = 188;
    private const double DefaultHeight = 104;

    private static readonly IReadOnlyList<SelectionOption> AttachModeOptionsInternal =
    [
        new("WindowAnchor", "附着到目标窗口")
    ];

    private static readonly IReadOnlyList<SelectionOption> ActionTypeOptionsInternal =
    [
        new("LeftClick", "鼠标单击"),
        new("DoubleClick", "鼠标双击"),
        new("RightClick", "鼠标右击"),
        new("MiddleClick", "鼠标中键"),
        new("WheelUp", "滚轮向上"),
        new("WheelDown", "滚轮向下"),
        new("TypeText", "输入文字"),
        new("Hotkey", "发送快捷键")
    ];

    private static readonly IReadOnlyList<SelectionOption> TargetModeOptionsInternal =
    [
        new("AnchorCenter", "点击视觉锚点中心"),
        new("OffsetFromAnchor", "点击锚点偏移位置"),
        new("AbsolutePoint", "点击绝对坐标点")
    ];

    private static readonly IReadOnlyList<SelectionOption> SearchRegionOptionsInternal =
    [
        new("Window", "只在当前窗口中搜索")
    ];

    private static readonly IReadOnlyList<SelectionOption> WaitTypeOptionsInternal =
    [
        new("Delay", "固定等待时间"),
        new("ImageAppear", "等待图片出现"),
        new("ImageDisappear", "等待图片消失"),
        new("PixelEquals", "等待像素变成指定颜色")
    ];

    private static readonly IReadOnlyList<SelectionOption> ConditionTypeOptionsInternal =
    [
        new("ImageExists", "判断图片是否存在"),
        new("PixelEquals", "判断像素是否等于指定颜色")
    ];

    private static readonly IReadOnlyList<SelectionOption> ExpectedStateOptionsInternal =
    [
        new("true", "满足条件时走 TRUE 分支"),
        new("false", "不满足条件时走 FALSE 分支")
    ];

    private static readonly IReadOnlyList<SelectionOption> PlaybackModeOptionsInternal =
    [
        new("Reusable", "可复用回放")
    ];

    private static readonly IReadOnlyList<SelectionOption> OnCompleteOptionsInternal =
    [
        new("Notify", "结束后保留通知标记")
    ];

    private readonly FlowNode _model;
    private bool _isSelected;
    private NodeStatus _status;
    private Brush _surfaceBrush = Brushes.Transparent;
    private Brush _borderBrush = Brushes.Transparent;
    private Brush _accentBrush = Brushes.Transparent;
    private Thickness _borderThicknessValue = new(1);
    private string _settingsEditorText = string.Empty;
    private double _opacityValue = 1.0d;

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
        IsTemporarilyDisabled
            ? "已停用"
            : Status switch
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

    public double OpacityValue
    {
        get => _opacityValue;
        private set => SetProperty(ref _opacityValue, value);
    }

    public bool IsTemporarilyDisabled
    {
        get => _model.IsTemporarilyDisabled;
        set
        {
            if (_model.IsTemporarilyDisabled == value)
            {
                return;
            }

            _model.IsTemporarilyDisabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusLabel));
            UpdatePalette();
        }
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
            RefreshSettingBindings();
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

    public string ConfigurationGuide =>
        Kind switch
        {
            FlowNodeKind.Start => "开始节点用于声明流程从哪里进入。它本身通常不做复杂动作，更多是告诉程序要附着到哪个窗口、用什么方式识别当前运行环境。",
            FlowNodeKind.Action => "动作节点用于真正执行鼠标或键盘操作。配置时先确定“动作类型”，再确定“目标点从哪里来”，最后按需要补上文字、热键、偏移量等附加参数。",
            FlowNodeKind.Vision => "视觉节点用于在屏幕上找图。通常流程是先给它绑定截图模板，再设置识别阈值、搜索范围、超时和重试次数，让后面的动作节点能拿到一个可靠锚点。",
            FlowNodeKind.Wait => "等待节点用于控制节奏。它既可以单纯等待几秒，也可以一直等到图片出现、图片消失、或者某个像素变成指定颜色后再继续。",
            FlowNodeKind.Condition => "条件节点用于做分支判断。它不会执行点击，而是判断当前屏幕是否满足某个条件，然后决定往 TRUE 分支还是 FALSE 分支继续。",
            FlowNodeKind.SubFlow => "录制片段节点用于回放一整段录制好的操作。它适合做兜底流程、复杂拖拽、临时没有建成结构化节点的操作片段。",
            FlowNodeKind.End => "结束节点用于收尾。当前版本主要承担“流程在这里结束”的作用，也可以预留后续通知、收集结果等动作。",
            _ => "当前节点支持 key=value 参数配置。"
        };

    public string ConfigurationExample =>
        Kind switch
        {
            FlowNodeKind.Start => "TargetWindow=EXCEL\r\nAttachMode=WindowAnchor",
            FlowNodeKind.Action => "ActionType=LeftClick\r\nTargetMode=AnchorCenter\r\nClickOffset=0,0",
            FlowNodeKind.Vision => "Threshold=0.92\r\nSearchRegion=Window",
            FlowNodeKind.Wait => "WaitType=Delay\r\nDurationMs=3000",
            FlowNodeKind.Condition => "ConditionType=ImageExists\r\nExpectedState=true\r\nThreshold=0.92",
            FlowNodeKind.SubFlow => "Clip=RecordedSequence01\r\nPlaybackMode=Reusable\r\nRecordedEventCount=0",
            FlowNodeKind.End => "OnComplete=Notify",
            _ => string.Empty
        };

    public string ParameterDefinitionsText =>
        Kind switch
        {
            FlowNodeKind.Start => "TargetWindow\r\n作用：目标窗口标题关键字或进程标识，流程开始时优先附着到这个窗口。\r\n常见写法：EXCEL、Chrome、企业微信\r\n何时填写：你希望流程只在某个应用窗口里运行时填写。\r\n\r\nAttachMode\r\n作用：附着方式，决定程序如何理解“当前目标窗口”。\r\n可填值：WindowAnchor\r\n建议：当前先保持 WindowAnchor，不建议乱改。",
            FlowNodeKind.Action => "ActionType\r\n作用：动作类型，决定这个节点到底执行什么。\r\n可填值：LeftClick（单击）、DoubleClick（双击）、RightClick（右击）、MiddleClick（中键）、WheelUp（滚轮上）、WheelDown（滚轮下）、TypeText（输入文字）、Hotkey（组合键）\r\n何时必填：始终建议填写。\r\n\r\nTargetMode\r\n作用：目标点来源，决定鼠标点到哪里。\r\n可填值：AnchorCenter（点视觉锚点中心）、OffsetFromAnchor（点锚点偏移位置）、AbsolutePoint（点绝对坐标）\r\n如何理解：\r\nAnchorCenter 适合“找到按钮后点它正中间”；\r\nOffsetFromAnchor 适合“找到图标后向右偏 20,0 再点击”；\r\nAbsolutePoint 适合固定坐标场景。\r\n\r\nClickOffset\r\n作用：偏移量，格式为 x,y。\r\n例子：0,0 表示不偏移；20,0 表示向右 20 像素；0,-10 表示向上 10 像素。\r\n何时必填：TargetMode=OffsetFromAnchor 时建议填写。\r\n\r\nPoint\r\n作用：绝对屏幕坐标，格式为 x,y。\r\n何时必填：TargetMode=AbsolutePoint 时必填。\r\n最快填写方式：先点上方“手动定位”，系统会自动写入。\r\n\r\nText\r\n作用：要输入的文字内容。\r\n何时必填：ActionType=TypeText 时必填。\r\n\r\nHotkey\r\n作用：组合键字符串。\r\n常见写法：Ctrl+V、Ctrl+S、Alt+Tab\r\n何时必填：ActionType=Hotkey 时必填。\r\n\r\nBeforeActionDelayMs\r\n作用：动作执行前先等待多久，单位毫秒。\r\n用途：等按钮浮现、菜单展开、焦点切换完成。\r\n\r\nAfterActionDelayMs\r\n作用：动作执行后再等待多久，单位毫秒。\r\n用途：等界面动画、弹窗、页面刷新完成，避免下一步太快。",
            FlowNodeKind.Vision => "Threshold\r\n作用：图像匹配相似度阈值，越高越严格。\r\n常见范围：0.88-0.96\r\n经验建议：0.92 左右最常用；识别太松会误判，识别太严会找不到。\r\n\r\nSearchRegion\r\n作用：搜索范围。\r\n可填值：Window\r\n说明：当前版本建议保持 Window。\r\n\r\nTimeoutMs\r\n作用：最长等待识别多久，单位毫秒。\r\n例子：10000 表示最多找 10 秒。\r\n\r\nRetryLimit\r\n作用：识别失败后的重试次数。\r\n例子：2 表示第一次失败后再尝试 2 次。\r\n\r\n截图模板\r\n作用：真正拿来匹配屏幕的图片。\r\n如何设置：点击“截图定位”或“粘贴图片”后自动绑定。\r\n注意：没有模板时，这个节点无法工作。",
            FlowNodeKind.Wait => "WaitType\r\n作用：等待方式。\r\n可填值：Delay（固定延时）、ImageAppear（等待图片出现）、ImageDisappear（等待图片消失）、PixelEquals（等待像素等于指定颜色）\r\n\r\nDurationMs\r\n作用：固定等待时长，单位毫秒。\r\n何时必填：WaitType=Delay 时必填。\r\n\r\nPoint\r\n作用：要观察的像素坐标，格式为 x,y。\r\n何时必填：WaitType=PixelEquals 时必填。\r\n\r\nPixelColor\r\n作用：目标颜色，十六进制色值。\r\n例子：#FFFFFF、#2F6FED\r\n何时必填：WaitType=PixelEquals 时必填。\r\n\r\nTolerance\r\n作用：颜色容差，数值越大越宽松。\r\n建议：8-20 之间通常够用。\r\n何时必填：WaitType=PixelEquals 时建议填写。\r\n\r\nThreshold\r\n作用：图片等待时的匹配阈值。\r\n何时必填：WaitType=ImageAppear 或 ImageDisappear 时建议填写。\r\n\r\n截图模板\r\n作用：用于判断“图片是否出现/消失”。\r\n何时必填：WaitType=ImageAppear 或 ImageDisappear 时必填。",
            FlowNodeKind.Condition => "ConditionType\r\n作用：判断方式。\r\n可填值：ImageExists（图片存在）、PixelEquals（像素等于指定颜色）\r\n\r\nExpectedState\r\n作用：预览模式下优先走哪条分支。\r\n可填值：true 或 false\r\n如何理解：true 表示预览时优先走 TRUE 分支；false 表示预览时优先走 FALSE 分支。真实执行时还是按实际识别结果决定。\r\n\r\nPoint\r\n作用：像素判断坐标，格式为 x,y。\r\n何时必填：ConditionType=PixelEquals 时必填。\r\n\r\nPixelColor\r\n作用：目标像素颜色。\r\n何时必填：ConditionType=PixelEquals 时必填。\r\n\r\nTolerance\r\n作用：像素颜色容差。\r\n何时必填：ConditionType=PixelEquals 时建议填写。\r\n\r\nThreshold\r\n作用：图片匹配阈值。\r\n何时必填：ConditionType=ImageExists 时建议填写。\r\n\r\n截图模板\r\n作用：用于判断目标图片是否存在。\r\n何时必填：ConditionType=ImageExists 时必填。",
            FlowNodeKind.SubFlow => "Clip\r\n作用：这段录制片段的名字，主要给人看，方便区分。\r\n例子：LoginFallback、关闭弹窗片段。\r\n\r\nPlaybackMode\r\n作用：播放模式标记。\r\n可填值：Reusable。\r\n建议：当前保持默认。\r\n\r\nRecordedEventCount\r\n作用：录制事件数量。\r\n来源：录制完成后系统自动填写，一般不需要手改。\r\n\r\nRecordedDurationMs\r\n作用：录制总时长，单位毫秒。\r\n来源：录制完成后系统自动填写，一般不需要手改。\r\n\r\n录制数据\r\n作用：真正的鼠标键盘轨迹。\r\n如何设置：先选中节点，再点“开始录制”，录完按 F9 停止。",
            FlowNodeKind.End => "OnComplete\r\n作用：流程结束后的预留动作。\r\n可填值：Notify。\r\n说明：当前版本里它主要是一个预留字段，先保持默认即可。",
            _ => "参数按 key=value 填写。左边是参数名，右边是参数值。"
        };

    public bool IsStartNode => Kind == FlowNodeKind.Start;

    public bool IsActionNode => Kind == FlowNodeKind.Action;

    public bool IsVisionNode => Kind == FlowNodeKind.Vision;

    public bool IsWaitNode => Kind == FlowNodeKind.Wait;

    public bool IsConditionNode => Kind == FlowNodeKind.Condition;

    public bool IsSubFlowNode => Kind == FlowNodeKind.SubFlow;

    public bool IsEndNode => Kind == FlowNodeKind.End;

    public IReadOnlyList<SelectionOption> AttachModeOptions => AttachModeOptionsInternal;

    public IReadOnlyList<SelectionOption> ActionTypeOptions => ActionTypeOptionsInternal;

    public IReadOnlyList<SelectionOption> TargetModeOptions => TargetModeOptionsInternal;

    public IReadOnlyList<SelectionOption> SearchRegionOptions => SearchRegionOptionsInternal;

    public IReadOnlyList<SelectionOption> WaitTypeOptions => WaitTypeOptionsInternal;

    public IReadOnlyList<SelectionOption> ConditionTypeOptions => ConditionTypeOptionsInternal;

    public IReadOnlyList<SelectionOption> ExpectedStateOptions => ExpectedStateOptionsInternal;

    public IReadOnlyList<SelectionOption> PlaybackModeOptions => PlaybackModeOptionsInternal;

    public IReadOnlyList<SelectionOption> OnCompleteOptions => OnCompleteOptionsInternal;

    public string TargetWindow
    {
        get => GetSetting("TargetWindow");
        set => SetSetting("TargetWindow", value);
    }

    public string AttachMode
    {
        get => GetSetting("AttachMode", "WindowAnchor");
        set => SetSetting("AttachMode", value, nameof(AttachMode));
    }

    public string ActionType
    {
        get => GetSetting("ActionType", "LeftClick");
        set => SetSetting(valueKey: "ActionType", value: value, dependentPropertyNames:
        [
            nameof(ActionType),
            nameof(ShowActionTextInput),
            nameof(ShowActionHotkeyInput)
        ]);
    }

    public string TargetMode
    {
        get => GetSetting("TargetMode", "AnchorCenter");
        set => SetSetting(valueKey: "TargetMode", value: value, dependentPropertyNames:
        [
            nameof(TargetMode),
            nameof(ShowActionOffsetInput),
            nameof(ShowActionPointInput)
        ]);
    }

    public string ClickOffset
    {
        get => GetSetting("ClickOffset", "0,0");
        set => SetSetting("ClickOffset", value);
    }

    public string BeforeActionDelayMsValue
    {
        get => GetSetting("BeforeActionDelayMs", "0");
        set => SetSetting("BeforeActionDelayMs", value);
    }

    public string AfterActionDelayMsValue
    {
        get => GetSetting("AfterActionDelayMs", "0");
        set => SetSetting("AfterActionDelayMs", value);
    }

    public string PointValue
    {
        get => GetSetting("Point");
        set => SetSetting(valueKey: "Point", value: value, dependentPropertyNames:
        [
            nameof(PointValue)
        ]);
    }

    public string ActionTextValue
    {
        get => GetSetting("Text");
        set => SetSetting("Text", value);
    }

    public string HotkeyValue
    {
        get => GetSetting("Hotkey", "Ctrl+V");
        set => SetSetting("Hotkey", value);
    }

    public string ThresholdValue
    {
        get => GetSetting("Threshold", "0.92");
        set => SetSetting("Threshold", value);
    }

    public string SearchRegion
    {
        get => GetSetting("SearchRegion", "Window");
        set => SetSetting("SearchRegion", value);
    }

    public string WaitType
    {
        get => GetSetting("WaitType", "Delay");
        set => SetSetting(valueKey: "WaitType", value: value, dependentPropertyNames:
        [
            nameof(WaitType),
            nameof(ShowWaitDurationInput),
            nameof(ShowWaitImageOptions),
            nameof(ShowWaitPixelOptions)
        ]);
    }

    public string DurationMsValue
    {
        get => GetSetting("DurationMs", "3000");
        set => SetSetting("DurationMs", value);
    }

    public string PixelColorValue
    {
        get => GetSetting("PixelColor", "#FFFFFF");
        set => SetSetting("PixelColor", value);
    }

    public string ToleranceValue
    {
        get => GetSetting("Tolerance", "12");
        set => SetSetting("Tolerance", value);
    }

    public string ConditionType
    {
        get => GetSetting("ConditionType", "ImageExists");
        set => SetSetting(valueKey: "ConditionType", value: value, dependentPropertyNames:
        [
            nameof(ConditionType),
            nameof(ShowConditionImageOptions),
            nameof(ShowConditionPixelOptions)
        ]);
    }

    public string ExpectedState
    {
        get => GetSetting("ExpectedState", "true");
        set => SetSetting("ExpectedState", value);
    }

    public string ClipName
    {
        get => GetSetting("Clip", "RecordedSequence01");
        set => SetSetting("Clip", value);
    }

    public string PlaybackMode
    {
        get => GetSetting("PlaybackMode", "Reusable");
        set => SetSetting("PlaybackMode", value);
    }

    public string RecordedEventCountText => GetSetting("RecordedEventCount", _model.RecordedEvents.Count.ToString());

    public string RecordedDurationMsText => GetSetting("RecordedDurationMs", _model.RecordedEvents.Sum(item => item.DelayMs).ToString());

    public string OnCompleteAction
    {
        get => GetSetting("OnComplete", "Notify");
        set => SetSetting("OnComplete", value);
    }

    public bool ShowActionTextInput => IsActionNode && ActionType.Equals("TypeText", StringComparison.OrdinalIgnoreCase);

    public bool ShowActionHotkeyInput => IsActionNode && ActionType.Equals("Hotkey", StringComparison.OrdinalIgnoreCase);

    public bool ShowActionOffsetInput => IsActionNode && TargetMode.Equals("OffsetFromAnchor", StringComparison.OrdinalIgnoreCase);

    public bool ShowActionPointInput => IsActionNode && TargetMode.Equals("AbsolutePoint", StringComparison.OrdinalIgnoreCase);

    public bool ShowWaitDurationInput => IsWaitNode && WaitType.Equals("Delay", StringComparison.OrdinalIgnoreCase);

    public bool ShowWaitImageOptions =>
        IsWaitNode && (WaitType.Equals("ImageAppear", StringComparison.OrdinalIgnoreCase)
            || WaitType.Equals("ImageDisappear", StringComparison.OrdinalIgnoreCase));

    public bool ShowWaitPixelOptions => IsWaitNode && WaitType.Equals("PixelEquals", StringComparison.OrdinalIgnoreCase);

    public bool ShowConditionImageOptions => IsConditionNode && ConditionType.Equals("ImageExists", StringComparison.OrdinalIgnoreCase);

    public bool ShowConditionPixelOptions => IsConditionNode && ConditionType.Equals("PixelEquals", StringComparison.OrdinalIgnoreCase);

    public void RefreshFromModel()
    {
        _settingsEditorText = BuildSettingsEditorText();
        OnPropertyChanged(nameof(SettingsEditorText));
        OnPropertyChanged(nameof(TimeoutMs));
        OnPropertyChanged(nameof(RetryLimit));
        OnPropertyChanged(nameof(AssetSummary));
        OnPropertyChanged(nameof(IsTemporarilyDisabled));
        OnPropertyChanged(nameof(StatusLabel));
        RefreshSettingBindings();
        UpdatePalette();
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

    private string GetSetting(string key, string fallback = "") =>
        _model.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private void SetSetting(string valueKey, string? value, params string[] dependentPropertyNames)
    {
        var normalized = value?.Trim() ?? string.Empty;
        var current = GetSetting(valueKey);
        if (string.Equals(current, normalized, StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            _model.Settings.Remove(valueKey);
        }
        else
        {
            _model.Settings[valueKey] = normalized;
        }

        _settingsEditorText = BuildSettingsEditorText();
        OnPropertyChanged(nameof(SettingsEditorText));
        OnPropertyChanged(nameof(SettingsSummary));

        foreach (var propertyName in dependentPropertyNames)
        {
            OnPropertyChanged(propertyName);
        }

        RefreshSettingBindings();
    }

    private void RefreshSettingBindings()
    {
        OnPropertyChanged(nameof(SettingsSummary));
        OnPropertyChanged(nameof(TargetWindow));
        OnPropertyChanged(nameof(AttachMode));
        OnPropertyChanged(nameof(ActionType));
        OnPropertyChanged(nameof(TargetMode));
        OnPropertyChanged(nameof(ClickOffset));
        OnPropertyChanged(nameof(BeforeActionDelayMsValue));
        OnPropertyChanged(nameof(AfterActionDelayMsValue));
        OnPropertyChanged(nameof(PointValue));
        OnPropertyChanged(nameof(ActionTextValue));
        OnPropertyChanged(nameof(HotkeyValue));
        OnPropertyChanged(nameof(ThresholdValue));
        OnPropertyChanged(nameof(SearchRegion));
        OnPropertyChanged(nameof(WaitType));
        OnPropertyChanged(nameof(DurationMsValue));
        OnPropertyChanged(nameof(PixelColorValue));
        OnPropertyChanged(nameof(ToleranceValue));
        OnPropertyChanged(nameof(ConditionType));
        OnPropertyChanged(nameof(ExpectedState));
        OnPropertyChanged(nameof(ClipName));
        OnPropertyChanged(nameof(PlaybackMode));
        OnPropertyChanged(nameof(RecordedEventCountText));
        OnPropertyChanged(nameof(RecordedDurationMsText));
        OnPropertyChanged(nameof(OnCompleteAction));
        OnPropertyChanged(nameof(ShowActionTextInput));
        OnPropertyChanged(nameof(ShowActionHotkeyInput));
        OnPropertyChanged(nameof(ShowActionOffsetInput));
        OnPropertyChanged(nameof(ShowActionPointInput));
        OnPropertyChanged(nameof(ShowWaitDurationInput));
        OnPropertyChanged(nameof(ShowWaitImageOptions));
        OnPropertyChanged(nameof(ShowWaitPixelOptions));
        OnPropertyChanged(nameof(ShowConditionImageOptions));
        OnPropertyChanged(nameof(ShowConditionPixelOptions));
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
            _ when IsTemporarilyDisabled => ColorFromHex("#64748B"),
            NodeStatus.Running => ColorFromHex("#FBBF24"),
            NodeStatus.Succeeded => ColorFromHex("#34D399"),
            NodeStatus.Failed => ColorFromHex("#F87171"),
            _ => IsSelected ? ColorFromHex("#E2E8F0") : ColorFromHex("#334155")
        };

        SurfaceBrush = new SolidColorBrush(surfaceColor);
        AccentBrush = new SolidColorBrush(accentColor);
        BorderBrush = new SolidColorBrush(borderColor);
        BorderThicknessValue = IsSelected || Status != NodeStatus.Idle ? new Thickness(2) : new Thickness(1);
        OpacityValue = IsTemporarilyDisabled ? 0.52d : 1.0d;
    }

    private static Color ColorFromHex(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex);
}
