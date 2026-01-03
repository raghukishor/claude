namespace Rbac.DataPlane.Diagnostics;

public class DiagnosticsContext
{
    private static readonly AsyncLocal<DiagnosticsContext> _current = new();

    public string RequestId { get; set; } = string.Empty;

    public static DiagnosticsContext Current
    {
        get => _current.Value ?? (_current.Value = new DiagnosticsContext());
        set => _current.Value = value;
    }
}
