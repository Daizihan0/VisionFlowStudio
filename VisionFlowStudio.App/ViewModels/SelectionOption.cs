namespace VisionFlowStudio.App.ViewModels;

public sealed class SelectionOption
{
    public SelectionOption(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; }

    public string Label { get; }
}
