using System.Drawing;

namespace VisionFlowStudio.App.Services;

/// <summary>
/// Windows 桌面通知服务
/// </summary>
public sealed class ToastNotificationService : IDisposable
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private bool _disposed;

    /// <summary>
    /// 显示成功通知
    /// </summary>
    public void ShowSuccess(string title, string message)
    {
        ShowNotification(title, message, System.Windows.Forms.ToolTipIcon.Info);
    }

    /// <summary>
    /// 显示错误通知
    /// </summary>
    public void ShowError(string title, string message)
    {
        ShowNotification(title, message, System.Windows.Forms.ToolTipIcon.Error);
    }

    /// <summary>
    /// 显示警告通知
    /// </summary>
    public void ShowWarning(string title, string message)
    {
        ShowNotification(title, message, System.Windows.Forms.ToolTipIcon.Warning);
    }

    private void ShowNotification(string title, string message, System.Windows.Forms.ToolTipIcon icon)
    {
        EnsureNotifyIcon();

        _notifyIcon!.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.Visible = true;
        _notifyIcon.ShowBalloonTip(5000);
    }

    private void EnsureNotifyIcon()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "VisionFlow Studio",
            Visible = false
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
    }
}
