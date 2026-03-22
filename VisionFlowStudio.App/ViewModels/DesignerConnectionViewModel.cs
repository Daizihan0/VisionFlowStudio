using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using VisionFlowStudio.Core.Models;

namespace VisionFlowStudio.App.ViewModels;

public sealed class DesignerConnectionViewModel : ObservableObject
{
    private readonly FlowConnection _model;

    public DesignerConnectionViewModel(FlowConnection model, FlowNodeViewModel sourceNode, FlowNodeViewModel targetNode)
    {
        _model = model;
        SourceNode = sourceNode;
        TargetNode = targetNode;

        SourceNode.PropertyChanged += NodeOnPropertyChanged;
        TargetNode.PropertyChanged += NodeOnPropertyChanged;
    }

    public FlowNodeViewModel SourceNode { get; }

    public FlowNodeViewModel TargetNode { get; }

    public FlowConnection Model => _model;

    public string Label =>
        !string.IsNullOrWhiteSpace(_model.Label)
            ? _model.Label
            : _model.ConnectorKind switch
            {
                FlowConnectorKind.True => "TRUE",
                FlowConnectorKind.False => "FALSE",
                FlowConnectorKind.Success => "SUCCESS",
                FlowConnectorKind.Failure => "FAIL",
                _ => "NEXT"
            };

    public double LabelX => (SourceNode.OutputX + TargetNode.InputX) / 2 - 18;

    public double LabelY => (SourceNode.OutputY + TargetNode.InputY) / 2 - 28;

    public Brush StrokeBrush =>
        new SolidColorBrush(
            _model.ConnectorKind switch
            {
                FlowConnectorKind.True => (Color)ColorConverter.ConvertFromString("#4ADE80"),
                FlowConnectorKind.False => (Color)ColorConverter.ConvertFromString("#F87171"),
                FlowConnectorKind.Success => (Color)ColorConverter.ConvertFromString("#38BDF8"),
                FlowConnectorKind.Failure => (Color)ColorConverter.ConvertFromString("#F97316"),
                _ => (Color)ColorConverter.ConvertFromString("#94A3B8")
            });

    public Geometry PathData
    {
        get
        {
            var start = new Point(SourceNode.OutputX, SourceNode.OutputY);
            var end = new Point(TargetNode.InputX, TargetNode.InputY);
            var delta = Math.Max(70, Math.Abs(end.X - start.X) * 0.45);

            var figure = new PathFigure
            {
                StartPoint = start,
                Segments =
                {
                    new BezierSegment(
                        new Point(start.X + delta, start.Y),
                        new Point(end.X - delta, end.Y),
                        end,
                        true)
                }
            };

            return new PathGeometry([figure]);
        }
    }

    private void NodeOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(FlowNodeViewModel.X)
            or nameof(FlowNodeViewModel.Y)
            or nameof(FlowNodeViewModel.InputX)
            or nameof(FlowNodeViewModel.InputY)
            or nameof(FlowNodeViewModel.OutputX)
            or nameof(FlowNodeViewModel.OutputY)))
        {
            return;
        }

        OnPropertyChanged(nameof(PathData));
        OnPropertyChanged(nameof(LabelX));
        OnPropertyChanged(nameof(LabelY));
    }
}
