using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;
using SMUI.Core.Services;

namespace SMUI.App.ViewModels;

/// <summary>自定义背景设置。</summary>
public partial class BackgroundSettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;

    public BackgroundSettingsViewModel(SettingsService settings, IDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
        Load();
    }

    [ObservableProperty] private string _newsBackground = "";
    [ObservableProperty] private string _categoryBackground = "";
    [ObservableProperty] private string _modItemBackground = "";

    /// <summary>背景变更通知（供页面即时刷新）。</summary>
    public event Action? BackgroundsChanged;

    private void Load()
    {
        NewsBackground = _settings["BGP_News"];
        CategoryBackground = _settings["BGP_Category"];
        ModItemBackground = _settings["BGP_ModItem"];
    }

    [RelayCommand]
    private async Task PickNewsAsync() => await PickAsync("BGP_News", v => { NewsBackground = v; });

    [RelayCommand]
    private async Task PickCategoryAsync() => await PickAsync("BGP_Category", v => { CategoryBackground = v; });

    [RelayCommand]
    private async Task PickModItemAsync() => await PickAsync("BGP_ModItem", v => { ModItemBackground = v; });

    [RelayCommand]
    private void ClearNews() => Clear("BGP_News", () => NewsBackground = "");

    [RelayCommand]
    private void ClearCategory() => Clear("BGP_Category", () => CategoryBackground = "");

    [RelayCommand]
    private void ClearModItem() => Clear("BGP_ModItem", () => ModItemBackground = "");

    private async Task PickAsync(string key, Action<string> apply)
    {
        var files = await _dialogs.PickFilesAsync("选择背景图片", "图片文件", "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp");
        if (files == null || files.Length == 0) return;
        _settings[key] = files[0];
        _settings.Save();
        apply(files[0]);
        BackgroundsChanged?.Invoke();
    }

    private void Clear(string key, Action reset)
    {
        _settings[key] = "";
        _settings.Save();
        reset();
        BackgroundsChanged?.Invoke();
    }
}
