using CommTT.Application.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Application.Services;

public class AlertEngine : IAlertEngine
{
    public event EventHandler<string>? AlertTriggered;
    public void Check(CommDataFrame frame)
    {
        // Design Doc §5.2: trigger alerts on abnormal frame size or patterns
        if (frame.Raw.Length > 4096)
            AlertTriggered?.Invoke(this, $"Oversized frame: {frame.Raw.Length} bytes");
    }
}
