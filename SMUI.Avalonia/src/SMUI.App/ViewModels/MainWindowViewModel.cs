using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;

namespace SMUI.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public HomeViewModel Home { get; }
    public ModsPageViewModel Mods { get; }
    public QueuePageViewModel Queue { get; }
    public UpdatesPageViewModel Updates { get; }
    public LogViewModel Log { get; }
    public SettingsPageViewModel Settings { get; }
    public ToolsViewModel Tools { get; }

    private readonly UiLogService _log;
    private readonly IDialogService _dialogs;

    public MainWindowViewModel(
        HomeViewModel home, ModsPageViewModel mods, QueuePageViewModel queue,
        UpdatesPageViewModel updates, LogViewModel log, SettingsPageViewModel settings,
        ToolsViewModel tools, UiLogService logService, IDialogService dialogs)
    {
        Home = home;
        Mods = mods;
        Queue = queue;
        Updates = updates;
        Log = log;
        Settings = settings;
        Tools = tools;
        _log = logService;
        _dialogs = dialogs;

        logService.EntryAdded += _ => { };
    }

    [ObservableProperty]
    private int _selectedPageIndex;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = "";

    [RelayCommand]
    public async Task LoadedAsync()
    {
        StatusText = "正在加载模组库...";
        await Mods.LoadAsync();
        Home.RefreshStatus();
        StatusText = "就绪";
        _log.Print("SMUI 已启动（Avalonia 跨平台版）", LogKind.Success);
    }
}
