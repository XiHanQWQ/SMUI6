using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;
using SMUI.Core.Services;

namespace SMUI.App.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly UiLogService _log;

    public BackgroundSettingsViewModel Backgrounds { get; }

    public SettingsPageViewModel(SettingsService settings, IDialogService dialogs, UiLogService log)
    {
        _settings = settings;
        _dialogs = dialogs;
        _log = log;
        Backgrounds = new BackgroundSettingsViewModel(settings, dialogs);
        Load();
    }

    public void Load()
    {
        GamePath = _settings.GamePath;
        RepositoryPath = _settings.RepositoryPath;
        BackupPath = _settings.GameBackupPath;
        VsCodePath = _settings["VisualStudioCodeEXE"];
        VsPath = _settings["VisualStudioEXE"];
        NexusKey = _settings["NexusAPI"];
        GithubToken = _settings["GithubToken"];
        GiteeToken = _settings["GiteeToken"];
        LaunchSelection = _settings["LaunchSelection"] != "2";
        LaunchParameters = _settings["LaunchParameters"];
        CustomLaunchCmd = _settings["CustomLaunchCMD"];
        AutoGetNews = _settings.GetBool("AutoGetNews");
        AutoCheckUpdate = _settings.GetBool("AutoCheckUpdate");
        AutoStartDownloadUpdate = _settings.GetBool("AutoStartDownloadUpdate");
        AutoSelectFirstNexusServer = _settings.GetBool("AutoSelectFirstNexusDownloadSever");
        SaveWindowSize = _settings.GetBool("SaveUserWindowSize");
        NexusPremium = _settings.GetBool("NexusPremium");
        OnPropertyChanged(string.Empty);
    }

    // 路径
    [ObservableProperty] private string _gamePath = "";
    [ObservableProperty] private string _repositoryPath = "";
    [ObservableProperty] private string _backupPath = "";
    [ObservableProperty] private string _vsCodePath = "";
    [ObservableProperty] private string _vsPath = "";

    // 在线服务
    [ObservableProperty] private string _nexusKey = "";
    [ObservableProperty] private bool _nexusPremium;
    [ObservableProperty] private string _githubToken = "";
    [ObservableProperty] private string _giteeToken = "";

    // 启动
    [ObservableProperty] private bool _launchSelection = true; // true=SMAPI false=自定义命令
    [ObservableProperty] private string _launchParameters = "";
    [ObservableProperty] private string _customLaunchCmd = "";

    // 开关
    [ObservableProperty] private bool _autoGetNews;
    [ObservableProperty] private bool _autoCheckUpdate;
    [ObservableProperty] private bool _autoStartDownloadUpdate;
    [ObservableProperty] private bool _autoSelectFirstNexusServer;
    [ObservableProperty] private bool _saveWindowSize;

    [RelayCommand]
    private async Task PickGamePathAsync()
    {
        // 自动识别（注册表 Steam/GOG + 全磁盘扫描 + 跨平台常见位置）
        var detected = await Task.Run(() => PathDetector.DetectGamePaths());
        var options = new List<string> { "📁 手动浏览文件夹..." };
        options.AddRange(detected.Select(p => $"🎮 {p}"));

        while (true)
        {
            var pick = await _dialogs.ChoiceAsync("选择星露谷游戏文件夹",
                $"已自动找到 {detected.Count} 个候选位置，选择其一或手动浏览：", options);
            if (pick < 0) return;
            if (pick == 0)
            {
                var folder = await _dialogs.PickFolderAsync("选择你的星露谷游戏文件夹");
                if (folder == null) continue;
                var error = ValidateGameFolder(folder);
                if (error != null)
                {
                    await _dialogs.InfoAsync("选择错误", error);
                    continue;
                }
                GamePath = folder;
                return;
            }
            GamePath = detected[pick - 1];
            return;
        }
    }

    [RelayCommand]
    private async Task PickRepositoryPathAsync()
    {
        var legacy = await Task.Run(() => PathDetector.DetectLegacyRepositoryPaths());
        var options = new List<string> { "✨ 创建新的模组数据仓库（选择空文件夹）...", "📁 手动浏览..." };
        options.AddRange(legacy.Select(l => $"📦 {l.Path}（{l.Source}）"));

        var pick = await _dialogs.ChoiceAsync("选择模组数据仓库",
            "可以选择现有仓库；如果你在用旧代产品（四代/五代）可直接选择其数据仓库。" +
            "建议放在固态硬盘并与游戏同一硬盘。", options);
        if (pick < 0) return;

        string target;
        if (pick == 0)
        {
            var folder = await _dialogs.PickFolderAsync("选择一个空文件夹，它将作为你的新数据仓库");
            if (folder == null) return;
            if (!await _dialogs.ConfirmAsync("确认使用此文件夹？", folder)) return;
            target = folder;
        }
        else if (pick == 1)
        {
            var folder = await _dialogs.PickFolderAsync("选择模组数据仓库文件夹");
            if (folder == null) return;
            target = folder;
        }
        else
        {
            target = legacy[pick - 2].Path;
        }

        RepositoryPath = target;
        InitializeRepository(target);
        await _dialogs.InfoAsync("已设置", "模组数据仓库已就绪。");
    }

    private static void InitializeRepository(string v)
    {
        // 初始化仓库结构（对应原版选择数据库路径）
        Directory.CreateDirectory(Path.Combine(v, "Default Sub Library"));
        Directory.CreateDirectory(Path.Combine(v, ".Download"));
        Directory.CreateDirectory(Path.Combine(v, ".Decompress"));
        var manifest = Path.Combine(v, "MANIFEST");
        if (!File.Exists(manifest)) File.WriteAllText(manifest, "This is your Mod Repository root path.");
    }

    [RelayCommand]
    private async Task PickBackupPathAsync()
    {
        var folder = await _dialogs.PickFolderAsync("选择游戏文件备份路径（使用文件替换类命令时必须，否则卸载会直接删除游戏文件）");
        if (folder != null) BackupPath = folder;
    }

    [RelayCommand]
    private async Task PickVsCodePath()
    {
        var files = await _dialogs.PickFilesAsync("选择 Visual Studio Code", "Code.exe", "*.exe");
        if (files is { Length: > 0 }) VsCodePath = files[0];
    }

    [RelayCommand]
    private async Task PickVsPath()
    {
        var files = await _dialogs.PickFilesAsync("选择 Visual Studio", "devenv.exe", "*.exe");
        if (files is { Length: > 0 }) VsPath = files[0];
    }

    private static string? ValidateGameFolder(string folder)
        => File.Exists(Path.Combine(folder, "Stardew Valley.exe")) || File.Exists(Path.Combine(folder, "StardewValley.exe"))
            ? null
            : "此文件夹路径下不包含星露谷的可执行文件：Stardew Valley.exe";

    /// <summary>设置保存后通知（供首页刷新状态）。</summary>
    public event Action? Saved;

    [RelayCommand]
    private void RegisterNxmProtocol()
    {
        var error = NxmProtocolService.Register();
        if (error == "")
        {
            _log.Print("nxm 下载协议已注册，NEXUS 网站的 nxm 链接将转接到 SMUI", LogKind.Success);
            _dialogs.InfoAsync("nxm 协议", "已注册 nxm 下载协议。");
        }
        else
        {
            _dialogs.InfoAsync("注册失败", error);
        }
    }

    [RelayCommand]
    private void RemoveNxmProtocol()
    {
        var error = NxmProtocolService.Remove();
        if (error == "")
        {
            _log.Print("nxm 下载协议已移除", LogKind.Info);
            _dialogs.InfoAsync("nxm 协议", "已移除 nxm 协议注册。");
        }
        else
        {
            _dialogs.InfoAsync("移除失败", error);
        }
    }

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        _settings.GamePath = GamePath;
        _settings.RepositoryPath = RepositoryPath;
        _settings.GameBackupPath = BackupPath;
        _settings["VisualStudioCodeEXE"] = VsCodePath;
        _settings["VisualStudioEXE"] = VsPath;
        _settings["NexusAPI"] = NexusKey;
        _settings["GithubToken"] = GithubToken;
        _settings["GiteeToken"] = GiteeToken;
        _settings["LaunchSelection"] = LaunchSelection ? "1" : "2";
        _settings["LaunchParameters"] = LaunchParameters;
        _settings["CustomLaunchCMD"] = CustomLaunchCmd;
        _settings.SetBool("AutoGetNews", AutoGetNews);
        _settings.SetBool("AutoCheckUpdate", AutoCheckUpdate);
        _settings.SetBool("AutoStartDownloadUpdate", AutoStartDownloadUpdate);
        _settings.SetBool("AutoSelectFirstNexusDownloadSever", AutoSelectFirstNexusServer);
        _settings.SetBool("SaveUserWindowSize", SaveWindowSize);
        _settings.SetBool("NexusPremium", NexusPremium);
        _settings.Save();
        _log.Print("设置已保存", LogKind.Success);
        Saved?.Invoke();
        await _dialogs.InfoAsync("已保存", "全部设置已写入配置文件。");
    }

    [RelayCommand]
    private async Task TestNexusKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(NexusKey))
        {
            await _dialogs.InfoAsync("NEXUS WEB API", "尚未填写 API Key。");
            return;
        }
        var nexus = new NexusApiService { ApiKey = NexusKey };
        var (info, error) = await nexus.ValidateAsync();
        if (error != "")
        {
            await _dialogs.InfoAsync("NEXUS WEB API", "验证失败：" + error);
            return;
        }
        await _dialogs.InfoAsync("NEXUS WEB API",
            $"用户名：{info!.Name}\n用户 ID：{info.UserId}\n邮箱：{info.Email}\n" +
            $"是否是会员：{(info.IsPremium ? "是" : "否")}\n是否是支持者：{(info.IsSupporter ? "是" : "否")}\n" +
            $"当前小时请求剩余量：{info.HourlyRemaining}/{info.HourlyLimit}\n" +
            $"今天内请求剩余量：{info.DailyRemaining}/{info.DailyLimit}");
    }
}
