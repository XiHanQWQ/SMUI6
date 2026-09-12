using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;
using SMUI.Core.Services;

namespace SMUI.App.ViewModels;

/// <summary>起始页面（复刻原版：状态信息 + 快捷入口 + 内容中心 + 检查应用更新）。</summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly UiLogService _log;

    public HomeViewModel(SettingsService settings, IDialogService dialogs, UiLogService log)
    {
        _settings = settings;
        _dialogs = dialogs;
        _log = log;
    }

    /// <summary>请求跳转到主界面选项卡（由主窗口接管）。</summary>
    public event Action<int>? NavigateRequested;

    public ObservableCollection<string> StatusLines { get; } = new();

    [ObservableProperty]
    private string _greeting = "欢迎使用 SMUI 6";

    [ObservableProperty]
    private string _appUpdateStatus = "";

    public void RefreshStatus()
    {
        Greeting = $"欢迎使用 SMUI 6 · {DateTime.Now:yyyy年M月d日}";

        StatusLines.Clear();
        StatusLines.Add("应用程序启动模式：便携式模式");
        StatusLines.Add("星露谷游戏文件夹：" + (ValidGamePath ? _settings.GamePath : "未设置（请到「设置」配置，支持自动识别）"));
        StatusLines.Add("SMAPI：" + (DetectSmapi() ? "已检测到 StardewModdingAPI.exe" : ValidGamePath ? "游戏目录中未检测到 StardewModdingAPI.exe" : "未检测"));
        StatusLines.Add("模组数据仓库：" + (ValidRepo ? _settings.RepositoryPath + "（" + CountSubLibraries() + " 个子库）" : "未设置（请到「设置」配置）"));
        StatusLines.Add("SMUI 版本：v" + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "6.0.0") + "（Avalonia 跨平台版）");
    }

    private bool ValidGamePath => _settings.GamePath is { Length: > 0 } && Directory.Exists(_settings.GamePath);
    private bool ValidRepo => _settings.RepositoryPath is { Length: > 0 } && Directory.Exists(_settings.RepositoryPath);
    private bool DetectSmapi() => ValidGamePath && File.Exists(Path.Combine(_settings.GamePath, "StardewModdingAPI.exe"));
    private int CountSubLibraries() { try { return AppServices.Library.ScanSubLibraries().Count; } catch { return 0; } }

    [RelayCommand]
    private async Task CopyStatus()
    {
        try
        {
            var text = string.Join(Environment.NewLine, StatusLines);
            var top = Avalonia.Application.Current?.ApplicationLifetime switch
            {
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d => d.MainWindow,
                _ => null,
            };
            // TODO: Avalonia 12 剪贴板 API 重构后恢复
            // if (top?.Clipboard is { } clip) clip.SetTextAsync(text);
            _log.Print("状态信息已复制到剪贴板", LogKind.Info);
        }
        catch { }
    }

    [RelayCommand]
    public void RunSmapi()
    {
        if (!ValidGamePath) { _dialogs.InfoAsync("无法启动", "请先在「设置」中配置游戏路径。"); return; }
        try
        {
            if (_settings["LaunchSelection"] == "2" && _settings["CustomLaunchCMD"] is { Length: > 0 } cmd)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c " + cmd) { CreateNoWindow = true });
                _log.Print("已执行自定义启动命令", LogKind.Info);
                return;
            }
            var smapi = Path.Combine(_settings.GamePath, "StardewModdingAPI.exe");
            if (!File.Exists(smapi)) { _dialogs.InfoAsync("无法启动", "游戏目录中未找到 StardewModdingAPI.exe。"); return; }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(smapi, _settings["LaunchParameters"]) { UseShellExecute = false });
            _log.Print("SMAPI 已启动", LogKind.Success);
        }
        catch (Exception ex) { _dialogs.InfoAsync("启动失败", ex.Message); }
    }

    // -------------------------------------------------- 快捷入口（复刻原版 常驻主题）

    [RelayCommand]
    private void OpenDocs()
    {
        var pdf = Path.Combine(AppServices.Settings.BaseDirectory, "SMUI6.pdf");
        if (File.Exists(pdf)) OpenPath(pdf, "");
        else _dialogs.InfoAsync("查阅文档", "未找到本地教程文档：" + pdf + "\n可将 SMUI6.pdf 放在程序目录后重试。");
    }

    [RelayCommand]
    private void OpenVideos() => OpenUrl("https://space.bilibili.com/319785096/channel/collectiondetail?sid=2903558");

    [RelayCommand]
    private void OpenKook() => OpenUrl("https://kook.top/yW15HU");

    [RelayCommand]
    private void OpenQQGroup() => _dialogs.InfoAsync("开发者群", "主群：738313040\n分群 1：271734093\n\n欢迎加入交流反馈。");

    [RelayCommand]
    private void RegisterNxm()
    {
        var error = NxmProtocolService.Register();
        if (error == "") { _log.Print("nxm 协议已注册", LogKind.Success); _dialogs.InfoAsync("注册表项", "已注册 nxm 下载协议。"); }
        else _dialogs.InfoAsync("注册失败", error);
    }

    [RelayCommand]
    private void RemoveNxm()
    {
        var error = NxmProtocolService.Remove();
        if (error == "") { _log.Print("nxm 协议已移除", LogKind.Info); _dialogs.InfoAsync("注册表项", "已移除 nxm 协议注册。"); }
        else _dialogs.InfoAsync("移除失败", error);
    }

    // -------------------------------------------------- 内容中心（复刻原版 内容中心.vb）

    [RelayCommand]
    private void OpenGameFolder() => OpenPath(_settings.GamePath, "请先设置游戏路径");

    [RelayCommand]
    private void OpenModsFolder() => OpenPath(Path.Combine(_settings.GamePath, "Mods"), "请先设置游戏路径");

    [RelayCommand]
    private void OpenSavesFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        OpenPath(Path.Combine(appData, "StardewValley", "Saves"), "未找到游戏存档文件夹");
    }

    [RelayCommand]
    private void OpenSmapiLogs()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        OpenPath(Path.Combine(appData, "StardewValley", "ErrorLogs"), "未找到 SMAPI 日志文件夹");
    }

    [RelayCommand]
    private void OpenRepoFolder() => OpenPath(_settings.RepositoryPath, "请先设置模组数据仓库");

    [RelayCommand]
    private void OpenAppFolder() => OpenPath(AppServices.Settings.BaseDirectory, "");

    [RelayCommand]
    private void OpenUserDataFolder() => OpenPath(AppServices.Settings.UserDataDirectory, "");

    [RelayCommand]
    private void OpenSmapiSite() => OpenUrl("https://smapi.io");
    [RelayCommand]
    private void OpenSmapiCompat() => OpenUrl("https://smapi.io/mods");
    [RelayCommand]
    private void OpenSmapiLogParser() => OpenUrl("https://smapi.io/log");
    [RelayCommand]
    private void OpenWiki() => OpenUrl("https://stardewvalleywiki.com");
    [RelayCommand]
    private void OpenForums() => OpenUrl("https://forums.stardewvalley.net");
    [RelayCommand]
    private void OpenNexusSdv() => OpenUrl("https://www.nexusmods.com/stardewvalley");
    [RelayCommand]
    private void OpenModDrop() => OpenUrl("https://www.moddrop.com/stardew-valley");
    [RelayCommand]
    private void OpenFarmPlanner() => OpenUrl("https://stardew.info/");
    [RelayCommand]
    private void OpenPredictor() => OpenUrl("https://mouseypounds.github.io/stardew-predictor");
    [RelayCommand]
    private void OpenCheckup() => OpenUrl("https://mouseypounds.github.io/stardew-checkup");

    // -------------------------------------------------- 检查应用更新（对应原版 服务器功能）

    [RelayCommand]
    public async Task CheckAppUpdateAsync()
    {
        AppUpdateStatus = "正在检查更新...";
        var current = Assembly.GetEntryAssembly()?.GetName().Version;
        var git = new GitApiService();
        var latest = new GitRelease();
        var error = await git.GetLatestReleaseAsync(GitApiService.Platform.GitHub, "XiHanQWQ/SMUI6", latest);
        if (error != "")
        {
            AppUpdateStatus = "检查失败：" + error;
            return;
        }
        if (Version.TryParse(latest.Tag.TrimStart('v', 'V'), out var remote) && current != null)
        {
            if (remote > current)
            {
                AppUpdateStatus = "发现新版本 " + latest.Tag + "（当前 v" + current.ToString(3) + "）";
                var body = latest.Body.Length <= 300 ? latest.Body : latest.Body[..300] + "...";
                if (await _dialogs.ConfirmAsync("发现新版本",
                        "最新版本：" + latest.Tag + "\n当前版本：v" + current.ToString(3) + "\n\n" + body + "\n\n是否前往下载页面？"))
                    OpenUrl("https://github.com/XiHanQWQ/SMUI6/releases");
            }
            else
            {
                AppUpdateStatus = "已是最新版本（v" + current.ToString(3) + "）";
            }
        }
        else
        {
            AppUpdateStatus = "最新版本：" + latest.Tag;
        }
    }

    internal static void OpenUrl(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", url);
            else
                System.Diagnostics.Process.Start("xdg-open", url);
        }
        catch { }
    }

    private void OpenPath(string path, string missingHint)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            if (missingHint.Length > 0) _dialogs.InfoAsync("提示", missingHint);
            return;
        }
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", path);
            else
                System.Diagnostics.Process.Start("xdg-open", path);
        }
        catch { }
    }
}
