using System.Runtime.InteropServices;
using System.Windows;
using Application = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;
using StickyNotes.Services.Interfaces;
using StickyNotes.Utilities;
using StickyNotes.Views;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace StickyNotes.Services;

/// <summary>
/// System tray icon backed by Windows.Forms.NotifyIcon. Provides a context
/// menu for creating, showing, hiding notes, opening settings, and exiting.
/// The icon is generated procedurally (no external .ico file required) and
/// its GDI handle is destroyed to prevent leaks.
/// </summary>
public sealed class SystemTrayService : ISystemTrayService
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly INoteWindowManager _windowManager;
    private readonly IServiceProvider _services;
    private Forms.NotifyIcon? _notifyIcon;
    private Drawing.Icon? _cachedIcon;
    private SettingsWindow? _settingsWindow;
    private bool _disposed;

    public SystemTrayService(INoteWindowManager windowManager, IServiceProvider services)
    {
        _windowManager = windowManager;
        _services = services;
    }

    public void Initialize()
    {
        if (_notifyIcon != null) return;

        _cachedIcon ??= CreateNoteIcon();

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "便签 (StickyNotes)",
            Visible = true,
            Icon = _cachedIcon
        };

        _notifyIcon.DoubleClick += (_, _) => SafeExec.Try(() => _windowManager.NewNote());

        BuildContextMenu();
        Logger.Info("System tray initialized.");
    }

    private void BuildContextMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add("新建便签", null, (_, _) => SafeExec.Try(() => _windowManager.NewNote()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("显示所有便签", null, (_, _) => SafeExec.Try(() => _windowManager.ShowAll()));
        menu.Items.Add("隐藏所有便签", null, (_, _) => SafeExec.Try(() => _windowManager.HideAll()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("设置...", null, (_, _) => SafeExec.Try(OpenSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            Logger.Info("Exit requested from tray.");
            Application.Current.Shutdown();
        });

        _notifyIcon!.ContextMenuStrip = menu;
    }

    private void OpenSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = _services.GetRequiredService<SettingsWindow>();
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Procedurally creates a 32x32 sticky-note icon (yellow square with a
    /// folded corner). Avoids shipping an .ico file.
    /// </summary>
    private static Drawing.Icon CreateNoteIcon()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using var g = Drawing.Graphics.FromImage(bitmap);
        g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;

        g.Clear(Drawing.Color.Transparent);

        // Main note body (yellow)
        using var bodyBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 249, 196));
        g.FillRectangle(bodyBrush, 4, 4, 24, 24);

        // Folded corner (bottom-right)
        var cornerPoints = new[]
        {
            new Drawing.Point(22, 28),
            new Drawing.Point(28, 22),
            new Drawing.Point(28, 28)
        };
        using var cornerBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 213, 79));
        g.FillPolygon(cornerBrush, cornerPoints);

        // Outline
        using var pen = new Drawing.Pen(Drawing.Color.FromArgb(93, 64, 55), 1.5f);
        g.DrawRectangle(pen, 4, 4, 24, 24);

        // "N" letter hint
        // (kept subtle — the icon reads as a note at 16px too)

        var hIcon = bitmap.GetHicon();
        var icon = Drawing.Icon.FromHandle(hIcon);
        var result = (Drawing.Icon)icon.Clone();
        DestroyIcon(hIcon); // release the original GDI handle
        icon.Dispose();
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _cachedIcon?.Dispose();
        _cachedIcon = null;
    }
}
