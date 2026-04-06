using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VisionFlowStudio.App.Services;
using VisionFlowStudio.Core.Models;
using VisionFlowStudio.Core.Services;

namespace VisionFlowStudio.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IProjectStorageService _projectStorageService;
    private readonly INodeTemplateLibraryService _nodeTemplateLibraryService;
    private readonly IFlowExecutionEngine _previewExecutionEngine;
    private readonly IFlowExecutionEngine _liveExecutionEngine;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly InputRecordingService _inputRecordingService;
    private readonly RelayCommand _runPreviewCommand;
    private readonly RelayCommand _runLiveCommand;
    private readonly RelayCommand _stopExecutionCommand;
    private readonly RelayCommand _startRecordingCommand;
    private readonly RelayCommand _stopRecordingCommand;
    private readonly RelayCommand _zoomOutCommand;
    private readonly RelayCommand _zoomInCommand;
    private readonly RelayCommand _resetZoomCommand;
    private readonly RelayCommand _deleteSelectedNodeCommand;
    private readonly RelayCommand _saveSelectedNodeAsTemplateCommand;
    private readonly RelayCommand _insertNodeTemplateCommand;
    private readonly RelayCommand _deleteNodeTemplateCommand;
    private readonly RelayCommand _beginConnectionCommand;
    private readonly RelayCommand _cancelConnectionCommand;
    private readonly RelayCommand _deleteSelectedConnectionCommand;
    private AutomationProject _project = new();
    private DesignerConnectionViewModel? _selectedConnection;
    private FlowNodeViewModel? _selectedNode;
    private FlowNodeViewModel? _pendingConnectionSource;
    private FlowNodeViewModel? _recordingTargetNode;
    private CancellationTokenSource? _executionCancellationTokenSource;
    private bool _isExecuting;
    private string _executionMode = "设计模式";
    private string? _currentFilePath;
    private double _zoomScale = 1.0;
    private double _designerCanvasUserWidth = 2200d;
    private double _designerCanvasUserHeight = 1400d;

    public MainViewModel(
        IProjectStorageService projectStorageService,
        INodeTemplateLibraryService nodeTemplateLibraryService,
        IFlowExecutionEngine previewExecutionEngine,
        IFlowExecutionEngine liveExecutionEngine,
        ScreenCaptureService screenCaptureService,
        InputRecordingService inputRecordingService)
    {
        _projectStorageService = projectStorageService;
        _nodeTemplateLibraryService = nodeTemplateLibraryService;
        _previewExecutionEngine = previewExecutionEngine;
        _liveExecutionEngine = liveExecutionEngine;
        _screenCaptureService = screenCaptureService;
        _inputRecordingService = inputRecordingService;

        Nodes = [];
        Connections = [];
        Logs = [];
        AssetItems = [];
        ScheduleItems = [];
        NodeTemplates = [];

        NewProjectCommand = new RelayCommand(CreateNewProject);
        SaveProjectCommand = new RelayCommand(async _ => await SaveProjectAsync());
        LoadProjectCommand = new RelayCommand(async _ => await LoadProjectAsync());
        AddVisionNodeCommand = new RelayCommand(_ => AddNode(FlowNodeKind.Vision));
        AddActionNodeCommand = new RelayCommand(_ => AddNode(FlowNodeKind.Action));
        AddWaitNodeCommand = new RelayCommand(_ => AddNode(FlowNodeKind.Wait));
        AddConditionNodeCommand = new RelayCommand(_ => AddNode(FlowNodeKind.Condition));
        AddSubFlowNodeCommand = new RelayCommand(_ => AddNode(FlowNodeKind.SubFlow));
        AddEndNodeCommand = new RelayCommand(_ => AddNode(FlowNodeKind.End));
        CaptureImageForNodeCommand = new RelayCommand(async _ => await CaptureImageForSelectedNodeAsync(), _ => SelectedNode is not null && !IsBusy);
        PasteImageForNodeCommand = new RelayCommand(_ => PasteClipboardImageToSelectedNode(), _ => SelectedNode is not null && !IsBusy);
        PickPointForNodeCommand = new RelayCommand(async _ => await PickPointForSelectedNodeAsync(), _ => SelectedNode is not null && !IsBusy);
        _startRecordingCommand = new RelayCommand(async _ => await StartRecordingAsync(), _ => !_inputRecordingService.IsRecording && !_isExecuting);
        _stopRecordingCommand = new RelayCommand(_ => StopRecording(), _ => _inputRecordingService.IsRecording);
        _runPreviewCommand = new RelayCommand(async _ => await RunEngineAsync(_previewExecutionEngine, "预览执行中"), _ => !_isExecuting && !_inputRecordingService.IsRecording);
        _runLiveCommand = new RelayCommand(async _ => await RunEngineAsync(_liveExecutionEngine, "真实执行中"), _ => !_isExecuting && !_inputRecordingService.IsRecording);
        _stopExecutionCommand = new RelayCommand(HandleEmergencyStop, () => _isExecuting || _inputRecordingService.IsRecording);
        _zoomOutCommand = new RelayCommand(_ => AdjustZoom(-0.1d));
        _zoomInCommand = new RelayCommand(_ => AdjustZoom(0.1d));
        _resetZoomCommand = new RelayCommand(_ => ZoomScale = 1.0d);
        _deleteSelectedNodeCommand = new RelayCommand(_ => DeleteSelectedNode(), _ => SelectedNode is not null && !IsBusy && SelectedNode.Kind != FlowNodeKind.Start);
        _saveSelectedNodeAsTemplateCommand = new RelayCommand(async _ => await SaveSelectedNodeAsTemplateAsync(), _ => SelectedNode is not null);
        _insertNodeTemplateCommand = new RelayCommand(parameter => InsertNodeTemplate(parameter as ReusableNodeTemplate), parameter => parameter is ReusableNodeTemplate && !IsBusy);
        _deleteNodeTemplateCommand = new RelayCommand(async parameter => await DeleteNodeTemplateAsync(parameter as ReusableNodeTemplate), parameter => parameter is ReusableNodeTemplate && !IsBusy);
        _beginConnectionCommand = new RelayCommand(_ => BeginConnectionFromSelectedNode(), _ => SelectedNode is not null && !IsBusy);
        _cancelConnectionCommand = new RelayCommand(_ => CancelConnectionMode(), _ => IsConnectionModeActive);
        _deleteSelectedConnectionCommand = new RelayCommand(_ => DeleteSelectedConnection(), _ => SelectedConnection is not null && !IsBusy);

        StartRecordingCommand = _startRecordingCommand;
        StopRecordingCommand = _stopRecordingCommand;
        RunPreviewCommand = _runPreviewCommand;
        RunLiveCommand = _runLiveCommand;
        StopExecutionCommand = _stopExecutionCommand;
        ZoomOutCommand = _zoomOutCommand;
        ZoomInCommand = _zoomInCommand;
        ResetZoomCommand = _resetZoomCommand;
        DeleteSelectedNodeCommand = _deleteSelectedNodeCommand;
        SaveSelectedNodeAsTemplateCommand = _saveSelectedNodeAsTemplateCommand;
        InsertNodeTemplateCommand = _insertNodeTemplateCommand;
        DeleteNodeTemplateCommand = _deleteNodeTemplateCommand;
        BeginConnectionCommand = _beginConnectionCommand;
        CancelConnectionCommand = _cancelConnectionCommand;
        DeleteSelectedConnectionCommand = _deleteSelectedConnectionCommand;

        CreateNewProject();
        _ = LoadNodeTemplatesAsync();
    }

    public ObservableCollection<FlowNodeViewModel> Nodes { get; }

    public ObservableCollection<DesignerConnectionViewModel> Connections { get; }

    public ObservableCollection<LogEntryViewModel> Logs { get; }

    public ObservableCollection<string> AssetItems { get; }

    public ObservableCollection<string> ScheduleItems { get; }

    public ObservableCollection<ReusableNodeTemplate> NodeTemplates { get; }

    public ICommand NewProjectCommand { get; }

    public ICommand SaveProjectCommand { get; }

    public ICommand LoadProjectCommand { get; }

    public ICommand AddVisionNodeCommand { get; }

    public ICommand AddActionNodeCommand { get; }

    public ICommand AddWaitNodeCommand { get; }

    public ICommand AddConditionNodeCommand { get; }

    public ICommand AddSubFlowNodeCommand { get; }

    public ICommand AddEndNodeCommand { get; }

    public ICommand CaptureImageForNodeCommand { get; }

    public ICommand PasteImageForNodeCommand { get; }

    public ICommand PickPointForNodeCommand { get; }

    public ICommand StartRecordingCommand { get; }

    public ICommand StopRecordingCommand { get; }

    public ICommand RunPreviewCommand { get; }

    public ICommand RunLiveCommand { get; }

    public ICommand StopExecutionCommand { get; }

    public ICommand ZoomOutCommand { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ResetZoomCommand { get; }

    public ICommand DeleteSelectedNodeCommand { get; }

    public ICommand SaveSelectedNodeAsTemplateCommand { get; }

    public ICommand InsertNodeTemplateCommand { get; }

    public ICommand DeleteNodeTemplateCommand { get; }

    public ICommand BeginConnectionCommand { get; }

    public ICommand CancelConnectionCommand { get; }

    public ICommand DeleteSelectedConnectionCommand { get; }

    public bool IsBusy => _isExecuting || _inputRecordingService.IsRecording;

    public double ZoomScale
    {
        get => _zoomScale;
        set
        {
            var clamped = Math.Clamp(value, 0.10d, 2.0d);
            if (!SetProperty(ref _zoomScale, clamped))
            {
                return;
            }

            OnPropertyChanged(nameof(ZoomPercentText));
        }
    }

    public string ZoomPercentText => $"{Math.Round(ZoomScale * 100)}%";

    public string ProjectName
    {
        get => _project.Name;
        set
        {
            if (_project.Name == value)
            {
                return;
            }

            _project.Name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HeaderSubtitle));
        }
    }

    public string ProjectDescription
    {
        get => _project.Description;
        set
        {
            if (_project.Description == value)
            {
                return;
            }

            _project.Description = value;
            OnPropertyChanged();
        }
    }

    public string HeaderSubtitle =>
        $"{NodeSummaryText} · {(_currentFilePath is null ? "未保存工程" : _currentFilePath)}";

    public string NodeSummaryText
    {
        get
        {
            var disabledCount = Nodes.Count(node => node.IsTemporarilyDisabled);
            return disabledCount == 0
                ? $"{Nodes.Count} 个节点 · {Connections.Count} 条连线"
                : $"{Nodes.Count} 个节点 · {Connections.Count} 条连线 · {disabledCount} 个已停用";
        }
    }

    public string LogSummaryText => $"共 {Logs.Count} 条记录";

    public string RunStateText => _executionMode;

    public double DesignerCanvasWidth =>
        Math.Max(_designerCanvasUserWidth, Nodes.Count == 0 ? 2200d : Nodes.Max(node => node.X + node.Width) + 420d);

    public double DesignerCanvasHeight =>
        Math.Max(_designerCanvasUserHeight, Nodes.Count == 0 ? 1400d : Nodes.Max(node => node.Y + node.Height) + 320d);

    public string NodeTemplateSummaryText =>
        NodeTemplates.Count == 0 ? "还没有保存任何复用节点" : $"{NodeTemplates.Count} 个可复用节点模板";

    public bool IsConnectionModeActive => _pendingConnectionSource is not null;

    public string ConnectionModeText =>
        _pendingConnectionSource is null
            ? "未开始手动连线"
            : $"连线中：{_pendingConnectionSource.Title} -> 请点击目标节点";

    public string SelectedNodeGuideTitle =>
        SelectedNode is null ? "参数说明" : $"{SelectedNode.DisplayKind}如何配置";

    public string SelectedNodeGuideText =>
        SelectedNode?.ConfigurationGuide ?? "选中节点后，这里会给出该节点的用途、关键参数和推荐配置方式。";

    public string SelectedNodeExampleText =>
        SelectedNode?.ConfigurationExample ?? "示例会按 key=value 格式显示，可直接参考填写到上面的参数编辑框。";

    public string SelectedNodeParameterDefinitionsText =>
        SelectedNode?.ParameterDefinitionsText ?? "选中节点后，这里会逐项解释每个参数名是什么意思、什么时候要填、可以填什么值。";

    public string SelectedNodeDisableHint =>
        "勾选“临时停用”后，运行时会跳过这个节点；如果它是某个分支入口，程序也不会再走入该分支。";

    public DesignerConnectionViewModel? SelectedConnection
    {
        get => _selectedConnection;
        private set
        {
            if (_selectedConnection == value)
            {
                return;
            }

            if (_selectedConnection is not null)
            {
                _selectedConnection.IsSelected = false;
            }

            _selectedConnection = value;

            if (_selectedConnection is not null)
            {
                _selectedConnection.IsSelected = true;
            }

            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public FlowNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode == value)
            {
                return;
            }

            if (_selectedNode is not null)
            {
                _selectedNode.IsSelected = false;
            }

            _selectedNode = value;

            if (_selectedNode is not null)
            {
                _selectedNode.IsSelected = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedNodeGuideTitle));
            OnPropertyChanged(nameof(SelectedNodeGuideText));
            OnPropertyChanged(nameof(SelectedNodeExampleText));
            OnPropertyChanged(nameof(SelectedNodeParameterDefinitionsText));
            RaiseCommandStates();
        }
    }

    public async Task HandleHotkeyAsync(int hotkeyId)
    {
        switch (hotkeyId)
        {
            case 1001:
                if (_inputRecordingService.IsRecording)
                {
                    StopRecording();
                }
                break;
            case 1002:
                HandleEmergencyStop();
                break;
        }

        await Task.CompletedTask;
    }

    public void AdjustZoom(double delta) => ZoomScale += delta;

    public void ResizeDesignerCanvas(double? width, double? height)
    {
        if (width is not null)
        {
            _designerCanvasUserWidth = Math.Max(2200d, width.Value);
        }

        if (height is not null)
        {
            _designerCanvasUserHeight = Math.Max(1400d, height.Value);
        }

        OnPropertyChanged(nameof(DesignerCanvasWidth));
        OnPropertyChanged(nameof(DesignerCanvasHeight));
    }

    public bool TryHandleNodeClick(FlowNodeViewModel clickedNode)
    {
        SelectedConnection = null;
        SelectedNode = clickedNode;

        if (!IsConnectionModeActive)
        {
            return false;
        }

        if (_pendingConnectionSource is null)
        {
            _pendingConnectionSource = clickedNode;
            RefreshConnectionModeState();
            return true;
        }

        if (_pendingConnectionSource.Id == clickedNode.Id)
        {
            Log("警告", "连线目标不能是当前源节点。", clickedNode.Id);
            return true;
        }

        var connectorKind = ResolveManualConnectorKind(_pendingConnectionSource);
        if (connectorKind is null)
        {
            Log("警告", $"节点“{_pendingConnectionSource.Title}”的可用输出已经连满了。", _pendingConnectionSource.Id);
            return true;
        }

        var label = connectorKind switch
        {
            FlowConnectorKind.True => "满足",
            FlowConnectorKind.False => "否则",
            _ => string.Empty
        };

        if (!CanCreateConnection(_pendingConnectionSource, clickedNode, connectorKind.Value, out var warning))
        {
            Log("警告", warning, _pendingConnectionSource.Id);
            return true;
        }

        AddConnection(_pendingConnectionSource, clickedNode, connectorKind.Value, label);
        Log("成功", $"已连接：{_pendingConnectionSource.Title} -> {clickedNode.Title}", _pendingConnectionSource.Id);
        CancelConnectionMode();
        return true;
    }

    public void SelectConnection(DesignerConnectionViewModel connectionViewModel)
    {
        SelectedConnection = connectionViewModel;
    }

    private void CreateNewProject()
    {
        _currentFilePath = null;
        ApplyProject(CreateStarterProject());
        Log("信息", "已创建新的流程设计工程。");
    }

    private void AddNode(FlowNodeKind kind)
    {
        var position = CalculateNewNodePosition();
        var node = new FlowNode
        {
            Kind = kind,
            Title = CreateNodeTitle(kind),
            Description = CreateNodeDescription(kind),
            Position = position,
            TimeoutMs = kind == FlowNodeKind.Wait ? 3_000 : 10_000,
            RetryLimit = kind is FlowNodeKind.Vision or FlowNodeKind.Condition ? 2 : 0,
            Settings = CreateDefaultSettings(kind)
        };

        AddNodeToProject(node, autoConnectFromSelection: true, $"已添加节点：{node.Title}");
    }

    private void AddNodeToProject(FlowNode node, bool autoConnectFromSelection, string successLog)
    {
        _project.Graph.Nodes.Add(node);
        var nodeViewModel = new FlowNodeViewModel(node);
        RegisterNodeViewModel(nodeViewModel);
        Nodes.Add(nodeViewModel);

        if (autoConnectFromSelection && SelectedNode is not null && SelectedNode.Id != nodeViewModel.Id)
        {
            var connectorKind = DetermineAutoConnector(SelectedNode);
            var label = connectorKind switch
            {
                FlowConnectorKind.True => "满足",
                FlowConnectorKind.False => "否则",
                _ => string.Empty
            };

            AddConnection(SelectedNode, nodeViewModel, connectorKind, label);
        }

        SelectedNode = nodeViewModel;
        RebuildAssetItems();
        RefreshDashboard();
        Log("信息", successLog, node.Id);
    }

    private async Task CaptureImageForSelectedNodeAsync()
    {
        if (SelectedNode is null)
        {
            return;
        }

        try
        {
            var image = await _screenCaptureService.CaptureUserRegionAsync();
            if (image is null)
            {
                Log("警告", "截图定位已取消。", SelectedNode.Id);
                return;
            }

            ApplyImageToNode(SelectedNode, image, $"{SelectedNode.Title}.png");
            Log("成功", $"已为节点“{SelectedNode.Title}”绑定截图模板。", SelectedNode.Id);
        }
        catch (Exception ex)
        {
            Log("错误", $"截图定位失败：{ex.Message}", SelectedNode.Id);
        }
    }

    private void PasteClipboardImageToSelectedNode()
    {
        if (SelectedNode is null)
        {
            return;
        }

        var image = _screenCaptureService.GetClipboardImage();
        if (image is null)
        {
            Log("警告", "剪贴板中没有可用图片。", SelectedNode.Id);
            return;
        }

        ApplyImageToNode(SelectedNode, image, $"{SelectedNode.Title}-clipboard.png");
        Log("成功", $"已把剪贴板图片绑定到节点“{SelectedNode.Title}”。", SelectedNode.Id);
    }

    private async Task PickPointForSelectedNodeAsync()
    {
        if (SelectedNode is null)
        {
            return;
        }

        try
        {
            var point = await _screenCaptureService.PickPointAsync();
            if (point is null)
            {
                Log("警告", "手动定位已取消。", SelectedNode.Id);
                return;
            }

            SelectedNode.Model.Settings["Point"] = $"{Math.Round(point.ScreenPoint.X)},{Math.Round(point.ScreenPoint.Y)}";
            SelectedNode.Model.Settings["PixelColor"] = point.ColorHex;
            SelectedNode.Model.Settings.TryAdd("Tolerance", "12");

            if (SelectedNode.Kind == FlowNodeKind.Action)
            {
                SelectedNode.Model.Settings["TargetMode"] = "AbsolutePoint";
            }
            else if (SelectedNode.Kind == FlowNodeKind.Condition)
            {
                SelectedNode.Model.Settings["ConditionType"] = "PixelEquals";
            }
            else if (SelectedNode.Kind == FlowNodeKind.Wait)
            {
                SelectedNode.Model.Settings["WaitType"] = "PixelEquals";
            }

            SelectedNode.RefreshFromModel();
            Log("成功", $"已记录点位 {SelectedNode.Model.Settings["Point"]}，色值 {point.ColorHex}。", SelectedNode.Id);
        }
        catch (Exception ex)
        {
            Log("错误", $"手动定位失败：{ex.Message}", SelectedNode.Id);
        }
    }

    private async Task StartRecordingAsync()
    {
        if (_inputRecordingService.IsRecording || _isExecuting)
        {
            return;
        }

        _recordingTargetNode = EnsureRecordingTargetNode();
        SelectedNode = _recordingTargetNode;
        _inputRecordingService.Start();
        _executionMode = "录制中（按 F9 停止）";
        RefreshDashboard();
        Log("信息", $"开始录制节点“{_recordingTargetNode.Title}”，按 F9 停止录制。", _recordingTargetNode.Id);

        if (Application.Current.MainWindow is Window mainWindow)
        {
            mainWindow.WindowState = WindowState.Minimized;
        }

        await Task.Delay(160);
    }

    private void StopRecording()
    {
        if (!_inputRecordingService.IsRecording)
        {
            return;
        }

        var recordedEvents = _inputRecordingService.Stop();
        _executionMode = "设计模式";

        if (Application.Current.MainWindow is Window mainWindow)
        {
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }

        if (_recordingTargetNode is null)
        {
            RefreshDashboard();
            return;
        }

        _recordingTargetNode.Model.RecordedEvents = recordedEvents.ToList();
        _recordingTargetNode.Model.Settings["RecordedEventCount"] = _recordingTargetNode.Model.RecordedEvents.Count.ToString();
        _recordingTargetNode.Model.Settings["RecordedDurationMs"] = _recordingTargetNode.Model.RecordedEvents.Sum(item => item.DelayMs).ToString();
        _recordingTargetNode.RefreshFromModel();
        SelectedNode = _recordingTargetNode;
        RebuildAssetItems();
        RefreshDashboard();
        Log("成功", $"录制完成：{_recordingTargetNode.Model.RecordedEvents.Count} 个事件。", _recordingTargetNode.Id);
    }

    private async Task RunEngineAsync(IFlowExecutionEngine engine, string modeLabel)
    {
        if (_isExecuting || _inputRecordingService.IsRecording)
        {
            return;
        }

        var shouldHideMainWindow = ReferenceEquals(engine, _liveExecutionEngine);
        WindowState? previousWindowState = null;

        try
        {
            _isExecuting = true;
            _executionMode = modeLabel;
            _executionCancellationTokenSource = new CancellationTokenSource();
            ResetNodeStatuses();
            RefreshDashboard();

            if (shouldHideMainWindow)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (Application.Current.MainWindow is not Window window)
                    {
                        return;
                    }

                    previousWindowState = window.WindowState;
                    window.WindowState = WindowState.Minimized;
                });

                await Task.Delay(220, _executionCancellationTokenSource.Token);
            }

            await engine.ExecutePreviewAsync(
                _project,
                entry => App.Current.Dispatcher.Invoke(() => Log(entry.Level, entry.Message, entry.NodeId)),
                (nodeId, status) => App.Current.Dispatcher.Invoke(() => UpdateNodeStatus(nodeId, status)),
                _executionCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Log("警告", "执行已被用户停止。");
        }
        catch (Exception ex)
        {
            Log("错误", $"运行失败：{ex.Message}");
        }
        finally
        {
            if (shouldHideMainWindow)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (Application.Current.MainWindow is not Window window)
                    {
                        return;
                    }

                    window.WindowState = previousWindowState ?? WindowState.Normal;
                    window.Activate();
                });
            }

            _isExecuting = false;
            _executionMode = "设计模式";
            _executionCancellationTokenSource?.Dispose();
            _executionCancellationTokenSource = null;
            RefreshDashboard();
        }
    }

    private void HandleEmergencyStop()
    {
        if (_inputRecordingService.IsRecording)
        {
            StopRecording();
            Log("警告", "已通过热键停止录制。", _recordingTargetNode?.Id);
        }

        if (_isExecuting)
        {
            _executionCancellationTokenSource?.Cancel();
            Log("警告", "已通过 F10 紧急停止当前执行。", SelectedNode?.Id);
        }
    }

    private async Task SaveProjectAsync()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "VisionFlow Project (*.vfs.json)|*.vfs.json|JSON 文件 (*.json)|*.json",
                FileName = string.IsNullOrWhiteSpace(ProjectName) ? "VisionFlowProject.vfs.json" : $"{ProjectName}.vfs.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _currentFilePath = dialog.FileName;
            await _projectStorageService.SaveAsync(_project, _currentFilePath, CancellationToken.None);
            RefreshDashboard();
            Log("成功", $"工程已保存到：{_currentFilePath}");
        }
        catch (Exception ex)
        {
            Log("错误", $"保存失败：{ex.Message}");
        }
    }

    private async Task LoadProjectAsync()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "VisionFlow Project (*.vfs.json)|*.vfs.json|JSON 文件 (*.json)|*.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var project = await _projectStorageService.LoadAsync(dialog.FileName, CancellationToken.None);
            _currentFilePath = dialog.FileName;
            ApplyProject(project);
            Log("成功", $"已加载工程：{dialog.FileName}");
        }
        catch (Exception ex)
        {
            Log("错误", $"打开失败：{ex.Message}");
        }
    }

    private async Task SaveSelectedNodeAsTemplateAsync()
    {
        if (SelectedNode is null)
        {
            return;
        }

        try
        {
            var existingTemplate = NodeTemplates.FirstOrDefault(template =>
                string.Equals(template.Name, SelectedNode.Title, StringComparison.OrdinalIgnoreCase));

            var template = new ReusableNodeTemplate
            {
                Id = existingTemplate?.Id ?? Guid.NewGuid(),
                Name = SelectedNode.Title,
                Description = string.IsNullOrWhiteSpace(SelectedNode.Description)
                    ? $"{SelectedNode.DisplayKind}模板"
                    : SelectedNode.Description,
                SavedAtUtc = DateTime.UtcNow,
                Node = CloneNode(SelectedNode.Model, resetIdentity: false, positionOverride: new ProjectPoint())
            };

            template.Node.IsTemporarilyDisabled = false;

            await _nodeTemplateLibraryService.SaveAsync(template, CancellationToken.None);
            await LoadNodeTemplatesAsync();
            Log("成功", existingTemplate is null
                ? $"已把节点“{SelectedNode.Title}”保存到复用节点库。"
                : $"已更新复用节点“{SelectedNode.Title}”。", SelectedNode.Id);
        }
        catch (Exception ex)
        {
            Log("错误", $"保存复用节点失败：{ex.Message}", SelectedNode.Id);
        }
    }

    private void InsertNodeTemplate(ReusableNodeTemplate? template)
    {
        if (template is null)
        {
            return;
        }

        var node = CloneNode(template.Node, resetIdentity: true, positionOverride: CalculateNewNodePosition());
        node.Title = CreateUniqueNodeTitle(template.Name);
        node.Description = string.IsNullOrWhiteSpace(template.Description) ? node.Description : template.Description;
        AddNodeToProject(node, autoConnectFromSelection: true, $"已从模板插入节点：{node.Title}");
    }

    private async Task LoadNodeTemplatesAsync()
    {
        try
        {
            var templates = await _nodeTemplateLibraryService.LoadAllAsync(CancellationToken.None);
            NodeTemplates.Clear();

            foreach (var template in templates.OrderByDescending(item => item.SavedAtUtc))
            {
                NodeTemplates.Add(template);
            }

            OnPropertyChanged(nameof(NodeTemplateSummaryText));
            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            Log("错误", $"加载复用节点库失败：{ex.Message}");
        }
    }

    private async Task DeleteNodeTemplateAsync(ReusableNodeTemplate? template)
    {
        if (template is null)
        {
            return;
        }

        try
        {
            await _nodeTemplateLibraryService.DeleteAsync(template.Id, CancellationToken.None);
            await LoadNodeTemplatesAsync();
            Log("成功", $"已删除复用节点“{template.Name}”。");
        }
        catch (Exception ex)
        {
            Log("错误", $"删除复用节点失败：{ex.Message}");
        }
    }

    private void BeginConnectionFromSelectedNode()
    {
        if (SelectedNode is null)
        {
            return;
        }

        _pendingConnectionSource = SelectedNode;
        RefreshConnectionModeState();
        Log("信息", $"已进入手动连线模式：请点击目标节点，源节点为“{SelectedNode.Title}”。", SelectedNode.Id);
    }

    private void CancelConnectionMode()
    {
        if (_pendingConnectionSource is null)
        {
            return;
        }

        _pendingConnectionSource = null;
        RefreshConnectionModeState();
    }

    private void DeleteSelectedConnection()
    {
        if (SelectedConnection is null)
        {
            return;
        }

        var connectionToDelete = SelectedConnection;
        _project.Graph.Connections.Remove(connectionToDelete.Model);
        Connections.Remove(connectionToDelete);
        SelectedConnection = null;
        OnPropertyChanged(nameof(NodeSummaryText));
        OnPropertyChanged(nameof(HeaderSubtitle));
        Log("成功", $"已删除连线：{connectionToDelete.SourceNode.Title} -> {connectionToDelete.TargetNode.Title}");
    }

    private void DeleteSelectedNode()
    {
        if (SelectedNode is null)
        {
            return;
        }

        var nodeToDelete = SelectedNode;
        if (nodeToDelete.Kind == FlowNodeKind.Start)
        {
            Log("警告", "开始节点不能删除。", nodeToDelete.Id);
            return;
        }

        var removedConnections = _project.Graph.Connections
            .Where(connection => connection.SourceNodeId == nodeToDelete.Id || connection.TargetNodeId == nodeToDelete.Id)
            .ToList();

        foreach (var connection in removedConnections)
        {
            _project.Graph.Connections.Remove(connection);
        }

        var connectionViewModels = Connections
            .Where(connection => connection.Model.SourceNodeId == nodeToDelete.Id || connection.Model.TargetNodeId == nodeToDelete.Id)
            .ToList();

        foreach (var connection in connectionViewModels)
        {
            Connections.Remove(connection);
        }

        _project.Graph.Nodes.Remove(nodeToDelete.Model);
        Nodes.Remove(nodeToDelete);

        SelectedConnection = null;

        if (_pendingConnectionSource?.Id == nodeToDelete.Id)
        {
            _pendingConnectionSource = null;
            RefreshConnectionModeState();
        }

        if (_recordingTargetNode?.Id == nodeToDelete.Id)
        {
            _recordingTargetNode = null;
        }

        SelectedNode = Nodes.FirstOrDefault();
        RebuildAssetItems();
        RefreshDashboard();
        Log("成功", $"已删除节点“{nodeToDelete.Title}”。", nodeToDelete.Id);
    }

    private void ApplyProject(AutomationProject project)
    {
        _project = project;
        _pendingConnectionSource = null;
        _recordingTargetNode = null;
        SelectedConnection = null;
        Nodes.Clear();
        Connections.Clear();
        Logs.Clear();
        AssetItems.Clear();
        ScheduleItems.Clear();

        foreach (var node in _project.Graph.Nodes)
        {
            var nodeViewModel = new FlowNodeViewModel(node);
            RegisterNodeViewModel(nodeViewModel);
            Nodes.Add(nodeViewModel);
        }

        foreach (var connection in _project.Graph.Connections)
        {
            var source = Nodes.FirstOrDefault(node => node.Id == connection.SourceNodeId);
            var target = Nodes.FirstOrDefault(node => node.Id == connection.TargetNodeId);
            if (source is null || target is null)
            {
                continue;
            }

            Connections.Add(new DesignerConnectionViewModel(connection, source, target));
        }

        foreach (var item in BuildDefaultSchedules())
        {
            ScheduleItems.Add(item);
        }

        RebuildAssetItems();
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectDescription));
        SelectedNode = Nodes.FirstOrDefault();
        RefreshConnectionModeState();
        RefreshDashboard();
    }

    private void ApplyImageToNode(FlowNodeViewModel nodeViewModel, BitmapSource bitmapSource, string assetFileName)
    {
        nodeViewModel.Model.AssetPayloadBase64 = _screenCaptureService.EncodeToBase64(bitmapSource);
        nodeViewModel.Model.AssetFileName = assetFileName;
        nodeViewModel.Model.Settings["Threshold"] = nodeViewModel.Model.Settings.TryGetValue("Threshold", out var threshold) ? threshold : "0.92";

        if (nodeViewModel.Kind == FlowNodeKind.Wait)
        {
            nodeViewModel.Model.Settings.TryAdd("WaitType", "ImageAppear");
        }
        else if (nodeViewModel.Kind == FlowNodeKind.Condition)
        {
            nodeViewModel.Model.Settings.TryAdd("ConditionType", "ImageExists");
        }

        nodeViewModel.RefreshFromModel();
        RebuildAssetItems();
    }

    private void RebuildAssetItems()
    {
        AssetItems.Clear();

        var assets = Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Model.AssetPayloadBase64) || node.Model.RecordedEvents.Count > 0)
            .Select(node => $"{node.Title}：{node.AssetSummary}")
            .ToList();

        if (assets.Count == 0)
        {
            assets.Add("当前还没有绑定任何截图或录制资源");
        }

        assets.Add("手动定位：可把屏幕点位写入动作/条件/等待节点");
        assets.Add("复用节点：支持把当前节点保存到模板库并跨项目插入");
        assets.Add("节点停用：停用后运行时会跳过该节点及其分支入口");
        assets.Add("全局热键：F9 停止录制，F10 紧急停止执行");

        foreach (var item in assets)
        {
            AssetItems.Add(item);
        }
    }

    private FlowNodeViewModel EnsureRecordingTargetNode()
    {
        if (SelectedNode?.Kind == FlowNodeKind.SubFlow)
        {
            return SelectedNode;
        }

        AddNode(FlowNodeKind.SubFlow);
        return SelectedNode!;
    }

    private FlowConnectorKind? ResolveManualConnectorKind(FlowNodeViewModel source)
    {
        if (source.Kind != FlowNodeKind.Condition)
        {
            return FlowConnectorKind.Next;
        }

        var existingConnections = _project.Graph.Connections
            .Where(connection => connection.SourceNodeId == source.Id)
            .ToList();

        if (existingConnections.All(connection => connection.ConnectorKind != FlowConnectorKind.True))
        {
            return FlowConnectorKind.True;
        }

        if (existingConnections.All(connection => connection.ConnectorKind != FlowConnectorKind.False))
        {
            return FlowConnectorKind.False;
        }

        return null;
    }

    private bool CanCreateConnection(FlowNodeViewModel source, FlowNodeViewModel target, FlowConnectorKind connectorKind, out string message)
    {
        message = string.Empty;

        if (source.Id == target.Id)
        {
            message = "节点不能连到自己。";
            return false;
        }

        var existingConnections = _project.Graph.Connections
            .Where(connection => connection.SourceNodeId == source.Id)
            .ToList();

        if (connectorKind is FlowConnectorKind.True or FlowConnectorKind.False
            && existingConnections.Any(connection => connection.ConnectorKind == connectorKind))
        {
            message = connectorKind == FlowConnectorKind.True
                ? "这个条件节点的 TRUE 分支已经连接过了。"
                : "这个条件节点的 FALSE 分支已经连接过了。";
            return false;
        }

        if (connectorKind == FlowConnectorKind.Next
            && existingConnections.Any(connection => connection.ConnectorKind == FlowConnectorKind.Next && connection.TargetNodeId == target.Id))
        {
            message = "这两个节点之间已经有同样的连线了。";
            return false;
        }

        if (source.Kind != FlowNodeKind.Condition
            && existingConnections.Any(connection => connection.ConnectorKind is FlowConnectorKind.Next or FlowConnectorKind.Success))
        {
            message = "普通节点当前只允许一条主输出连线。";
            return false;
        }

        return true;
    }

    private void AddConnection(FlowNodeViewModel source, FlowNodeViewModel target, FlowConnectorKind connectorKind, string label)
    {
        if (!CanCreateConnection(source, target, connectorKind, out _))
        {
            return;
        }

        var connection = new FlowConnection
        {
            SourceNodeId = source.Id,
            TargetNodeId = target.Id,
            ConnectorKind = connectorKind,
            Label = label
        };

        _project.Graph.Connections.Add(connection);
        Connections.Add(new DesignerConnectionViewModel(connection, source, target));
        OnPropertyChanged(nameof(NodeSummaryText));
        OnPropertyChanged(nameof(HeaderSubtitle));
    }

    private void UpdateNodeStatus(Guid nodeId, NodeStatus status)
    {
        var node = Nodes.FirstOrDefault(item => item.Id == nodeId);
        if (node is null)
        {
            return;
        }

        node.Status = status;
    }

    private void ResetNodeStatuses()
    {
        foreach (var node in Nodes)
        {
            node.Status = NodeStatus.Idle;
        }
    }

    private void Log(string level, string message, Guid? nodeId = null)
    {
        Logs.Add(new LogEntryViewModel(new ExecutionLogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            NodeId = nodeId
        }));

        OnPropertyChanged(nameof(LogSummaryText));
    }

    private ProjectPoint CalculateNewNodePosition()
    {
        if (SelectedNode is not null)
        {
            return new ProjectPoint
            {
                X = SelectedNode.X + 260,
                Y = SelectedNode.Y + (SelectedNode.Kind == FlowNodeKind.Condition ? 140 : 0)
            };
        }

        if (Nodes.Count == 0)
        {
            return new ProjectPoint { X = 90, Y = 140 };
        }

        var maxX = Nodes.Max(node => node.X);
        var medianY = Nodes.OrderBy(node => node.Y).ElementAt(Nodes.Count / 2).Y;
        return new ProjectPoint { X = maxX + 250, Y = medianY };
    }

    private static FlowConnectorKind DetermineAutoConnector(FlowNodeViewModel source)
    {
        if (source.Kind != FlowNodeKind.Condition)
        {
            return FlowConnectorKind.Next;
        }

        return source.Model.Settings.TryGetValue("ExpectedState", out var value)
               && bool.TryParse(value, out var expectedState)
               && expectedState
            ? FlowConnectorKind.True
            : FlowConnectorKind.False;
    }

    private string CreateNodeTitle(FlowNodeKind kind)
    {
        var suffix = Nodes.Count(node => node.Kind == kind) + 1;
        return kind switch
        {
            FlowNodeKind.Action => $"动作 {suffix}",
            FlowNodeKind.Vision => $"视觉定位 {suffix}",
            FlowNodeKind.Wait => $"等待 {suffix}",
            FlowNodeKind.Condition => $"条件判断 {suffix}",
            FlowNodeKind.SubFlow => $"录制片段 {suffix}",
            FlowNodeKind.End => $"结束 {suffix}",
            _ => $"节点 {suffix}"
        };
    }

    private string CreateUniqueNodeTitle(string baseTitle)
    {
        if (Nodes.All(node => !string.Equals(node.Title, baseTitle, StringComparison.OrdinalIgnoreCase)))
        {
            return baseTitle;
        }

        var index = 2;
        while (true)
        {
            var candidate = $"{baseTitle} {index}";
            if (Nodes.All(node => !string.Equals(node.Title, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            index++;
        }
    }

    private static string CreateNodeDescription(FlowNodeKind kind) =>
        kind switch
        {
            FlowNodeKind.Action => "执行鼠标点击、双击、右键、滚轮或键盘输入。",
            FlowNodeKind.Vision => "使用截图模板、像素或窗口锚点确认当前位置。",
            FlowNodeKind.Wait => "等待指定时长或等待界面状态变化后继续。",
            FlowNodeKind.Condition => "根据视觉结果或像素判断走不同分支。",
            FlowNodeKind.SubFlow => "录制一段真实操作，并在流程中整段回放。",
            FlowNodeKind.End => "流程执行完毕后在这里收束。",
            _ => "配置当前节点行为。"
        };

    private static Dictionary<string, string> CreateDefaultSettings(FlowNodeKind kind) =>
        kind switch
        {
            FlowNodeKind.Action => new Dictionary<string, string>
            {
                ["ActionType"] = "LeftClick",
                ["TargetMode"] = "AnchorCenter",
                ["ClickOffset"] = "0,0",
                ["BeforeActionDelayMs"] = "0",
                ["AfterActionDelayMs"] = "0"
            },
            FlowNodeKind.Vision => new Dictionary<string, string>
            {
                ["Threshold"] = "0.92",
                ["SearchRegion"] = "Window"
            },
            FlowNodeKind.Wait => new Dictionary<string, string>
            {
                ["WaitType"] = "Delay",
                ["DurationMs"] = "3000"
            },
            FlowNodeKind.Condition => new Dictionary<string, string>
            {
                ["ConditionType"] = "ImageExists",
                ["ExpectedState"] = "true"
            },
            FlowNodeKind.SubFlow => new Dictionary<string, string>
            {
                ["Clip"] = "RecordedSequence01",
                ["PlaybackMode"] = "Reusable",
                ["RecordedEventCount"] = "0"
            },
            FlowNodeKind.End => new Dictionary<string, string>
            {
                ["OnComplete"] = "Notify"
            },
            _ => new Dictionary<string, string>()
        };

    private static IEnumerable<string> BuildDefaultSchedules() =>
    [
        "工作日 09:00 自动运行主流程（预留）",
        "每周一 08:30 运行日报导出流程（预留）"
    ];

    private static AutomationProject CreateStarterProject()
    {
        var startNode = new FlowNode
        {
            Kind = FlowNodeKind.Start,
            Title = "开始",
            Description = "流程从这里启动，可绑定目标窗口或预处理动作。",
            Position = new ProjectPoint { X = 90, Y = 240 },
            Settings = new Dictionary<string, string>
            {
                ["TargetWindow"] = "EXCEL",
                ["AttachMode"] = "WindowAnchor"
            }
        };

        var visionNode = new FlowNode
        {
            Kind = FlowNodeKind.Vision,
            Title = "查找登录按钮",
            Description = "通过截图模板或剪贴板中的图片定位目标控件。",
            Position = new ProjectPoint { X = 360, Y = 220 },
            RetryLimit = 2,
            Settings = new Dictionary<string, string>
            {
                ["Threshold"] = "0.93",
                ["SearchRegion"] = "Window"
            }
        };

        var clickNode = new FlowNode
        {
            Kind = FlowNodeKind.Action,
            Title = "单击登录",
            Description = "找到锚点后，在中心点偏移位置执行单击。",
            Position = new ProjectPoint { X = 650, Y = 220 },
            Settings = new Dictionary<string, string>
            {
                ["ActionType"] = "LeftClick",
                ["TargetMode"] = "AnchorCenter",
                ["ClickOffset"] = "0,0",
                ["BeforeActionDelayMs"] = "0",
                ["AfterActionDelayMs"] = "300"
            }
        };

        var waitNode = new FlowNode
        {
            Kind = FlowNodeKind.Wait,
            Title = "等待界面完成",
            Description = "等待图片消失或像素变色后再进入下一步。",
            Position = new ProjectPoint { X = 930, Y = 220 },
            TimeoutMs = 5_000,
            RetryLimit = 1,
            Settings = new Dictionary<string, string>
            {
                ["WaitType"] = "ImageDisappear",
                ["DurationMs"] = "5000",
                ["Threshold"] = "0.92"
            }
        };

        var conditionNode = new FlowNode
        {
            Kind = FlowNodeKind.Condition,
            Title = "判断是否进入主页",
            Description = "根据主页标识是否出现，分支到成功或重试子流程。",
            Position = new ProjectPoint { X = 1220, Y = 220 },
            RetryLimit = 1,
            Settings = new Dictionary<string, string>
            {
                ["ConditionType"] = "ImageExists",
                ["ExpectedState"] = "true",
                ["Threshold"] = "0.92"
            }
        };

        var subFlowNode = new FlowNode
        {
            Kind = FlowNodeKind.SubFlow,
            Title = "回放登录补救片段",
            Description = "如果失败，回放一段录制好的补救操作。",
            Position = new ProjectPoint { X = 1220, Y = 430 },
            Settings = new Dictionary<string, string>
            {
                ["Clip"] = "LoginFallback",
                ["PlaybackMode"] = "Reusable",
                ["RecordedEventCount"] = "0"
            }
        };

        var endNode = new FlowNode
        {
            Kind = FlowNodeKind.End,
            Title = "结束",
            Description = "输出运行结果并准备交给定时器或通知模块。",
            Position = new ProjectPoint { X = 1520, Y = 220 },
            Settings = new Dictionary<string, string>
            {
                ["OnComplete"] = "Notify"
            }
        };

        return new AutomationProject
        {
            Name = "办公流程自动化 Demo",
            Description = "演示截图锚点、动作、等待、分支和录制片段回放的节点式流程骨架。",
            Graph = new FlowGraph
            {
                Name = "主流程",
                Nodes = [startNode, visionNode, clickNode, waitNode, conditionNode, subFlowNode, endNode],
                Connections =
                [
                    new FlowConnection { SourceNodeId = startNode.Id, TargetNodeId = visionNode.Id, ConnectorKind = FlowConnectorKind.Next },
                    new FlowConnection { SourceNodeId = visionNode.Id, TargetNodeId = clickNode.Id, ConnectorKind = FlowConnectorKind.Success },
                    new FlowConnection { SourceNodeId = clickNode.Id, TargetNodeId = waitNode.Id, ConnectorKind = FlowConnectorKind.Next },
                    new FlowConnection { SourceNodeId = waitNode.Id, TargetNodeId = conditionNode.Id, ConnectorKind = FlowConnectorKind.Next },
                    new FlowConnection { SourceNodeId = conditionNode.Id, TargetNodeId = endNode.Id, ConnectorKind = FlowConnectorKind.True, Label = "成功" },
                    new FlowConnection { SourceNodeId = conditionNode.Id, TargetNodeId = subFlowNode.Id, ConnectorKind = FlowConnectorKind.False, Label = "失败" },
                    new FlowConnection { SourceNodeId = subFlowNode.Id, TargetNodeId = endNode.Id, ConnectorKind = FlowConnectorKind.Next, Label = "补救后继续" }
                ]
            }
        };
    }

    private void RefreshDashboard()
    {
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectDescription));
        OnPropertyChanged(nameof(NodeSummaryText));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(RunStateText));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(NodeTemplateSummaryText));
        OnPropertyChanged(nameof(SelectedNodeDisableHint));
        OnPropertyChanged(nameof(DesignerCanvasWidth));
        OnPropertyChanged(nameof(DesignerCanvasHeight));
        RaiseCommandStates();
    }

    private void RegisterNodeViewModel(FlowNodeViewModel nodeViewModel)
    {
        nodeViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(FlowNodeViewModel.IsTemporarilyDisabled)
                or nameof(FlowNodeViewModel.Title)
                or nameof(FlowNodeViewModel.Description))
            {
                OnPropertyChanged(nameof(NodeSummaryText));
                OnPropertyChanged(nameof(HeaderSubtitle));
            }

            if (args.PropertyName is nameof(FlowNodeViewModel.X)
                or nameof(FlowNodeViewModel.Y))
            {
                OnPropertyChanged(nameof(DesignerCanvasWidth));
                OnPropertyChanged(nameof(DesignerCanvasHeight));
            }
        };
    }

    private void RefreshConnectionModeState()
    {
        OnPropertyChanged(nameof(IsConnectionModeActive));
        OnPropertyChanged(nameof(ConnectionModeText));
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        _runPreviewCommand.RaiseCanExecuteChanged();
        _runLiveCommand.RaiseCanExecuteChanged();
        _stopExecutionCommand.RaiseCanExecuteChanged();
        _startRecordingCommand.RaiseCanExecuteChanged();
        _stopRecordingCommand.RaiseCanExecuteChanged();
        _deleteSelectedNodeCommand.RaiseCanExecuteChanged();
        _saveSelectedNodeAsTemplateCommand.RaiseCanExecuteChanged();
        _insertNodeTemplateCommand.RaiseCanExecuteChanged();
        _deleteNodeTemplateCommand.RaiseCanExecuteChanged();
        _beginConnectionCommand.RaiseCanExecuteChanged();
        _cancelConnectionCommand.RaiseCanExecuteChanged();
        _deleteSelectedConnectionCommand.RaiseCanExecuteChanged();
        ((RelayCommand)CaptureImageForNodeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PasteImageForNodeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PickPointForNodeCommand).RaiseCanExecuteChanged();
    }

    private static FlowNode CloneNode(FlowNode source, bool resetIdentity, ProjectPoint? positionOverride)
    {
        return new FlowNode
        {
            Id = resetIdentity ? Guid.NewGuid() : source.Id,
            Kind = source.Kind,
            Title = source.Title,
            Description = source.Description,
            Position = positionOverride ?? new ProjectPoint { X = source.Position.X, Y = source.Position.Y },
            TimeoutMs = source.TimeoutMs,
            RetryLimit = source.RetryLimit,
            Settings = source.Settings.ToDictionary(pair => pair.Key, pair => pair.Value),
            AssetPayloadBase64 = source.AssetPayloadBase64,
            AssetFileName = source.AssetFileName,
            RecordedEvents =
            [
                .. source.RecordedEvents.Select(item => new RecordedInputEvent
                {
                    EventType = item.EventType,
                    DelayMs = item.DelayMs,
                    X = item.X,
                    Y = item.Y,
                    MouseButton = item.MouseButton,
                    WheelDelta = item.WheelDelta,
                    VirtualKey = item.VirtualKey
                })
            ],
            IsTemporarilyDisabled = source.IsTemporarilyDisabled
        };
    }
}
