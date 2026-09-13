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
        InitStorageRows();
        _ = RefreshStorageAsync();
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
        IsEnglish = _settings["Language"] == "en-US";
        UseStandaloneWebview = _settings.GetBool("UseStandaloneWebView2");
        WebviewHardwareAccel = _settings.GetBool("WebView2HardwareAcceleration");
        WebviewRuntimeFolder = _settings["WebView2RuntimeFolder"];
        OnPropertyChanged(string.Empty);
    }

    // 路径
    /// <summary>设置二级菜单选中索引（复刻 WinForms UiTabControlMenu2：路径设置/地区和语言/在线服务/启动项/功能和数值/字体样式/存储管理/设置 WebView2/自定义图像）。</summary>
    [ObservableProperty] private int _sectionIndex;

    // ---- 地区和语言 ----
    [ObservableProperty] private bool _isEnglish;

    // ---- 设置 WebView2（同步 WinForms TabPage2） ----
    [ObservableProperty] private bool _useStandaloneWebview;
    [ObservableProperty] private bool _webviewHardwareAccel;
    [ObservableProperty] private string _webviewRuntimeFolder = "";

    // ---- 在线服务：切换显示 / 前往管理 ----
    /// <summary>NEXUS Key 密码字符（'●' 圆点 / '\0' 明文），「切换显示」按钮切换。</summary>
    [ObservableProperty] private char _nexusKeyChar = '●';

    [RelayCommand]
    private void ToggleKeyReveal() => NexusKeyChar = NexusKeyChar == '●' ? '\0' : '●';

    [RelayCommand]
    private void OpenNexusAccount()
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.nexusmods.com/users/myaccount?tab=api+access") { UseShellExecute = true });

    [RelayCommand]
    private Task PickWebviewRuntimeFolderAsync()
        => _dialogs.PickFolderAsync("选择 WebView2 Runtime 文件夹").ContinueWith(t =>
        {
            if (t.Result is string s) WebviewRuntimeFolder = s;
        });

    [RelayCommand]
    private void OpenWebviewDownload()
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });

    // ---- 存储管理：一比一复刻 WinForms ListView10 十项清单（清理空间.vb） ----
    public System.Collections.ObjectModel.ObservableCollection<StorageRowVm> StorageRows { get; } = new();
    [ObservableProperty] private StorageRowVm? _selectedStorageRow;
    [ObservableProperty] private string _storageCalcText = "计算模组数据库总数据大小";

    private static string FmtKb(long kb) => kb >= 1024 ? $"{kb / 1024.0:F1} MB" : $"{kb:F0} KB";

    private static long DirSize(string dir)
    {
        long total = 0;
        try
        {
            var d = new DirectoryInfo(dir);
            foreach (var f in d.EnumerateFiles("*", SearchOption.AllDirectories))
                try { total += f.Length; } catch { }
        }
        catch { }
        return total;
    }

    /// <summary>带排除目录名的大小统计（对应 共享方法.GetDirectorySizeWithSub）。</summary>
    private static long DirSizeEx(string dir, HashSet<string> exclude)
    {
        long total = 0;
        try
        {
            var root = new DirectoryInfo(dir);
            foreach (var f in root.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                bool skip = false;
                for (var p = f.Directory; p != null && p.FullName != root.FullName; p = p.Parent!)
                    if (exclude.Contains(p.Name)) { skip = true; break; }
                if (!skip) total += f.Length;
            }
        }
        catch { }
        return total;
    }

    private string AppDir => AppServices.Settings.BaseDirectory;
    private string UserData => AppServices.Settings.UserDataDirectory;
    private string EdgeDefault => Path.Combine(UserData, "WebView2Cache", "EBWebView", "Default");
    private string EdgeRoot => Path.Combine(UserData, "WebView2Cache", "EBWebView");
    private string InstallerPath => Path.Combine(UserData, "SMUI 6 Installer.exe");

    public void InitStorageRows()
    {
        StorageRows.Clear();
        var green = "#00C800";
        StorageRows.Add(new StorageRowVm("SMUI 中间型解释代码和二进制本体", green, "不可清理"));
        StorageRows.Add(new StorageRowVm("SMUI 所有组件完整容量", green, "不可清理"));
        StorageRows.Add(new StorageRowVm("已安装的插件", "#8080FF", "不可清理"));
        string[] names = { "检查更新下载的安装包", "下载的模组压缩包", "临时解压", "WebView2 主要缓存", "WebView2 Cookies", "WebView2 Service Worker", "WebView2 其他全部缓存" };
        foreach (var n in names) StorageRows.Add(new StorageRowVm(n));
    }

    [RelayCommand]
    private Task RefreshStorageAsync() => Task.Run(() =>
    {
        var r = StorageRows;
        if (r.Count == 0) InitStorageRows();

        long s1 = 0;
        try
        {
            foreach (var exe in new[] { Path.Combine(AppDir, "SMUI.exe"), Path.Combine(AppDir, "SMUI.dll") })
                if (File.Exists(exe)) s1 += new FileInfo(exe).Length;
        }
        catch { }
        r[0].StatusText = FmtKb(s1 / 1024);

        r[1].StatusText = FmtKb(DirSizeEx(AppDir, new HashSet<string> { "UserData" }) / 1024 / 1024);
        r[2].StatusText = FmtKb(DirSize(Path.Combine(UserData, "Plugin")) / 1024);

        s1 = 0;
        if (File.Exists(InstallerPath)) s1 += new FileInfo(InstallerPath).Length;
        for (int i = 1; i <= 3; i++)
        {
            var part = Path.Combine(UserData, $"SMUI 6 Installer.7z.{i:000}");
            if (File.Exists(part)) s1 += new FileInfo(part).Length;
        }
        r[3].StatusText = FmtKb(s1 / 1024 / 1024);

        var dl = Path.Combine(RepositoryPath, ".Download");
        r[4].StatusText = Directory.Exists(dl) ? FmtKb(DirSize(dl) / 1024 / 1024) : "无数据";
        var dc = Path.Combine(RepositoryPath, ".Decompress");
        r[5].StatusText = Directory.Exists(dc) ? FmtKb(DirSize(dc) / 1024 / 1024) : "无数据";

        static long DirPart(string p)
        { long s = 0; if (Directory.Exists(p)) s = DirSize(p); return s; }
        long cache = DirPart(Path.Combine(EdgeDefault, "Cache"))
                   + DirPart(Path.Combine(EdgeDefault, "Code Cache"))
                   + DirPart(Path.Combine(EdgeDefault, "DawnCache"))
                   + DirPart(Path.Combine(EdgeDefault, "GPUCache"))
                   + DirPart(Path.Combine(EdgeDefault, "IndexedDB"));
        r[6].StatusText = FmtKb(cache / 1024 / 1024);
        r[7].StatusText = FmtKb(DirPart(Path.Combine(EdgeDefault, "Network")) / 1024 / 1024);
        r[8].StatusText = FmtKb(DirPart(Path.Combine(EdgeDefault, "Service Worker")) / 1024 / 1024);

        long other = 0;
        if (Directory.Exists(EdgeDefault))
            other += DirSizeEx(EdgeDefault, new HashSet<string> { "Cache", "Code Cache", "DawnCache", "GPUCache", "IndexedDB", "Network", "Service Worker" });
        if (Directory.Exists(EdgeRoot))
            other += DirSizeEx(EdgeRoot, new HashSet<string> { "Default" });
        r[9].StatusText = FmtKb(other / 1024 / 1024);
    });

    [RelayCommand]
    private async Task CleanupStorageAsync()
    {
        var row = SelectedStorageRow;
        if (row == null) { await _dialogs.InfoAsync("清理选中项", "请先在列表中选中要清理的项目。"); return; }
        var idx = StorageRows.IndexOf(row);
        if (idx < 3) { await _dialogs.InfoAsync("清理选中项", "该项是 SMUI 运行必需的内容，不可清理。"); return; }
        if (!await _dialogs.ConfirmAsync("清理选中项", $"确定清理「{row.Name}」吗？此操作不可恢复。")) return;
        try
        {
            void DelDir(string? p) { if (p != null && Directory.Exists(p)) Directory.Delete(p, true); }
            void DelFile(string? p) { if (p != null && File.Exists(p)) File.Delete(p); }
            switch (idx)
            {
                case 3:
                    DelFile(InstallerPath);
                    for (int i = 1; i <= 3; i++) DelFile(Path.Combine(UserData, $"SMUI 6 Installer.7z.{i:000}"));
                    row.StatusText = "已清理";
                    break;
                case 4: DelDir(Path.Combine(RepositoryPath, ".Download")); row.StatusText = "已清理"; break;
                case 5: DelDir(Path.Combine(RepositoryPath, ".Decompress")); row.StatusText = "已清理"; break;
                case 6:
                    DelDir(Path.Combine(EdgeDefault, "Cache"));
                    DelDir(Path.Combine(EdgeDefault, "Code Cache"));
                    DelDir(Path.Combine(EdgeDefault, "DawnCache"));
                    DelDir(Path.Combine(EdgeDefault, "GPUCache"));
                    DelDir(Path.Combine(EdgeDefault, "IndexedDB"));
                    row.StatusText = "已清理";
                    break;
                case 7: DelDir(Path.Combine(EdgeDefault, "Network")); row.StatusText = "已清理"; break;
                case 8: DelDir(Path.Combine(EdgeDefault, "Service Worker")); row.StatusText = "已清理"; break;
                case 9:
                    var whitelist = new HashSet<string> { "Default", "Cache", "Code Cache", "DawnCache", "GPUCache", "IndexedDB", "Network", "Service Worker" };
                    foreach (var root in new[] { EdgeDefault, EdgeRoot })
                    {
                        if (!Directory.Exists(root)) continue;
                        foreach (var f in Directory.GetFiles(root)) if (!whitelist.Contains(Path.GetFileName(f))) File.Delete(f);
                        foreach (var d in Directory.GetDirectories(root)) if (!whitelist.Contains(Path.GetFileName(d))) Directory.Delete(d, true);
                    }
                    row.StatusText = "已清理";
                    break;
            }
            _log.Print($"存储管理：已清理「{row.Name}」", LogKind.Success);
        }
        catch (Exception ex) { await _dialogs.InfoAsync("清理失败", ex.Message); }
    }

    [RelayCommand]
    private void CalculateStorage()
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath) || !Directory.Exists(RepositoryPath))
        {
            StorageCalcText = "模组数据库总数据大小：无数据";
            return;
        }
        var size = DirSizeEx(RepositoryPath, new HashSet<string> { ".Download", ".Decompress" });
        StorageCalcText = $"模组数据库总数据大小：{size / 1024.0 / 1024.0:F1} MB";
    }

    [RelayCommand]
    private void OpenUserDataFolder2()
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UserData) { UseShellExecute = true });

    [RelayCommand]
    private void OpenAppFolder2()
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppDir) { UseShellExecute = true });

    [RelayCommand]
    private void OpenPluginFolder2()
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(UserData, "Plugin")) { UseShellExecute = true });

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
        _settings["Language"] = IsEnglish ? "en-US" : "zh-CN";
        _settings.SetBool("UseStandaloneWebView2", UseStandaloneWebview);
        _settings.SetBool("WebView2HardwareAcceleration", WebviewHardwareAccel);
        _settings["WebView2RuntimeFolder"] = WebviewRuntimeFolder;
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

/// <summary>存储管理清单行（复刻 WinForms ListView10 三列）。</summary>
public class StorageRowVm
{
    public string Name { get; }
    public string RowBrush { get; }
    public string StatusBrush { get; }
    public string StatusText { get; set; }

    public StorageRowVm(string name, string rowBrush = "", string statusText = "可以清理")
    {
        Name = name;
        RowBrush = string.IsNullOrEmpty(rowBrush) ? "#CDD6F4" : rowBrush;
        StatusBrush = rowBrush == "#8080FF" ? "#8080FF" : string.IsNullOrEmpty(rowBrush) ? "#A6ADC8" : rowBrush;
        StatusText = statusText;
    }
}
