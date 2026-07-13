using StickyNotes.Models;
using StickyNotes.Services.Interfaces;
using StickyNotes.Utilities;

namespace StickyNotes.ViewModels;

/// <summary>
/// ViewModel for the settings dialog. Wraps AppSettings properties for
/// two-way binding and persists changes via ISettingsService.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromCurrent();
        SaveCommand = new RelayCommand(_ => Save());
    }

    // ---------- Bound properties ----------

    private bool _autoStartWithWindows;
    public bool AutoStartWithWindows
    {
        get => _autoStartWithWindows;
        set => SetProperty(ref _autoStartWithWindows, value);
    }

    private bool _enableMethodA;
    public bool EnableMethodA
    {
        get => _enableMethodA;
        set => SetProperty(ref _enableMethodA, value);
    }

    private bool _enableMethodB;
    public bool EnableMethodB
    {
        get => _enableMethodB;
        set => SetProperty(ref _enableMethodB, value);
    }

    private MountTarget _mountTarget;
    public MountTarget MountTarget
    {
        get => _mountTarget;
        set => SetProperty(ref _mountTarget, value);
    }

    private NoteColor _defaultColor;
    public NoteColor DefaultColor
    {
        get => _defaultColor;
        set => SetProperty(ref _defaultColor, value);
    }

    private double _defaultWidth;
    public double DefaultWidth
    {
        get => _defaultWidth;
        set => SetProperty(ref _defaultWidth, value);
    }

    private double _defaultHeight;
    public double DefaultHeight
    {
        get => _defaultHeight;
        set => SetProperty(ref _defaultHeight, value);
    }

    private double _defaultFontSize;
    public double DefaultFontSize
    {
        get => _defaultFontSize;
        set => SetProperty(ref _defaultFontSize, value);
    }

    public RelayCommand SaveCommand { get; }

    // ---------- Helpers ----------

    private void LoadFromCurrent()
    {
        var s = _settingsService.Current;
        _autoStartWithWindows = _settingsService.IsAutoStartEnabled();
        _enableMethodA = s.EnableMethodA;
        _enableMethodB = s.EnableMethodB;
        _mountTarget = s.MountTarget;
        _defaultColor = s.DefaultColor;
        _defaultWidth = s.DefaultWidth;
        _defaultHeight = s.DefaultHeight;
        _defaultFontSize = s.DefaultFontSize;
    }

    private void Save()
    {
        var s = _settingsService.Current;
        s.EnableMethodA = _enableMethodA;
        s.EnableMethodB = _enableMethodB;
        s.MountTarget = _mountTarget;
        s.DefaultColor = _defaultColor;
        s.DefaultWidth = _defaultWidth;
        s.DefaultHeight = _defaultHeight;
        s.DefaultFontSize = _defaultFontSize;

        _settingsService.SetAutoStart(_autoStartWithWindows);
        s.AutoStartWithWindows = _autoStartWithWindows;
        _settingsService.Save();

        Logger.Info("Settings saved.");
    }
}
