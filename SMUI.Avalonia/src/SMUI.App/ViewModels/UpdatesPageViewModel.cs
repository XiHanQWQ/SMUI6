using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;
using SMUI.Core.Models;
using SMUI.Core.Services;

namespace SMUI.App.ViewModels;

/// <summary>检查更新结果行。</summary>
public class UpdateResultVm : ViewModelBase
{
    public string UniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public string InstalledVersion { get; set; } = "";
    public string SuggestedVersion { get; set; } = "";
    public string Compatibility { get; set; } = "";
    public string NexusId { get; set; } = "";
    public string ModDropId { get; set; } = "";
    public string GitHubRepo { get; set; } = "";
    public string Url { get; set; } = "";
    public string StatusText { get; set; } = "";

    public string VersionText =>
        string.IsNullOrEmpty(SuggestedVersion) ? "无更新建议" :
        string.IsNullOrEmpty(InstalledVersion) ? $"→ {SuggestedVersion}" :
        $"{InstalledVersion} → {SuggestedVersion}";
}

public partial class UpdatesPageViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly UiLogService _log;

    public UpdatesPageViewModel(SettingsService settings, IDialogService dialogs, UiLogService log)
    {
        _settings = settings;
        _dialogs = dialogs;
        _log = log;
    }

    public ObservableCollection<UpdateResultVm> Results { get; } = new();

    /// <summary>下载队列。</summary>
    public ObservableCollection<DownloadTaskVm> Downloads { get; } = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = "";

    [ObservableProperty]
    private UpdateResultVm? _selectedResult;

    // ------------------------------------------------- smapi.io 批量检查

    [RelayCommand]
    public async Task CheckAllUpdatesAsync()
    {
        var sub = _settings.LastSubLibrary;
        if (string.IsNullOrEmpty(sub))
        {
            await _dialogs.InfoAsync("无法检查", "请先在「模组管理」选择数据子库。");
            return;
        }

        IsBusy = true;
        BusyText = "正在收集所有模组项信息...";
        Results.Clear();
        try
        {
            var queries = new List<SmapiCloudService.ModQuery>();
            var itemPaths = new List<string>();
            var cats = AppServices.Library.ScanCategories(sub);
            foreach (var (cat, _) in cats)
            {
                foreach (var entry in AppServices.Library.ScanItems(sub, cat))
                {
                    var info = new ItemInfo();
                    info.Read(entry.ItemPath, new ItemInfo.ComputeFlags
                    {
                        UniqueId = true,
                        UpdateKeys = true,
                        InstalledVersion = true,
                    }, _settings.GamePath);
                    if (info.ErrorMessage != "" || (info.UniqueIds.Count == 0 && info.NexusIds.Count == 0)) continue;
                    var keys = new List<string>();
                    keys.AddRange(info.UniqueIds.Select(u => $"SMAPI:{u}"));
                    keys.AddRange(info.NexusIds.Select(n => $"Nexus:{n}"));
                    keys.AddRange(info.ModDropIds.Select(m => $"ModDrop:{m}"));
                    keys.AddRange(info.GitHubRepos.Select(g => $"GitHub:{g}"));
                    queries.Add(new SmapiCloudService.ModQuery(info.UniqueIds.FirstOrDefault() ?? entry.Name,
                        keys, info.InstalledVersions.FirstOrDefault() ?? ""));
                    itemPaths.Add(entry.ItemPath);
                }
            }

            if (queries.Count == 0)
            {
                await _dialogs.InfoAsync("检查更新", "当前子库没有可检查的模组项。");
                return;
            }

            BusyText = $"正在向 smapi.io 查询 {queries.Count} 个模组的更新...";
            var (results, error) = await AppServices.SmapiCloud.CheckModsAsync(queries);
            if (error != "")
            {
                await _dialogs.InfoAsync("检查失败", error);
                return;
            }

            foreach (var r in results)
            {
                Results.Add(new UpdateResultVm
                {
                    UniqueId = r.Id,
                    Name = string.IsNullOrEmpty(r.Name) ? r.Id : r.Name,
                    InstalledVersion = "",
                    SuggestedVersion = r.SuggestedVersion,
                    Compatibility = r.CompatibilitySummary,
                    NexusId = r.NexusId,
                    ModDropId = r.ModDropId,
                    GitHubRepo = r.GitHubRepo,
                    Url = r.SuggestedUrl,
                    StatusText = string.IsNullOrEmpty(r.SuggestedVersion) ? "已是最新" : "有更新",
                });
            }
            _log.Print($"检查完成：{Results.Count(r => r.SuggestedVersion != "")} 个模组有更新建议", LogKind.Success);
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    // ------------------------------------------------- 对选中项运行更新

    [RelayCommand]
    public async Task RunNexusUpdateAsync()
    {
        var idStr = SelectedResult?.NexusId;
        if (string.IsNullOrEmpty(idStr))
        {
            await _dialogs.InfoAsync("无法更新", "此模组没有 NEXUS 更新键。");
            return;
        }
        var item = await PickTargetItemAsync();
        if (item == null) return;
        await RunNexusUpdateAsync(idStr, item);
    }

    public async Task RunNexusUpdateAsync(string nexusId, string targetItemPath)
    {
        if (string.IsNullOrEmpty(_settings["NexusAPI"]))
        {
            await _dialogs.InfoAsync("需要 NEXUS API Key", "请先在「设置 → 在线服务」中配置 NEXUS API Key。");
            return;
        }

        IsBusy = true;
        BusyText = "正在获取 NEXUS 文件列表...";
        try
        {
            AppServices.Nexus.ApiKey = _settings["NexusAPI"];
            var (files, error) = await AppServices.Nexus.GetModFilesAsync(int.Parse(nexusId), "main");
            if (error != "")
            {
                await _dialogs.InfoAsync("NEXUS 错误", error);
                return;
            }
            if (files.Count == 0)
            {
                await _dialogs.InfoAsync("无文件", "该模组在 NEXUS 上没有主文件。");
                return;
            }

            var options = files
                .OrderByDescending(f => f.IsPrimary)
                .ThenByDescending(f => f.UploadedTime)
                .Select(f => $"{f.Name}  (v{f.Version}, {FormatSize(f.Size)})")
                .ToList();
            var pick = await _dialogs.ChoiceAsync("选择要下载的 NEXUS 文件",
                $"NEXUS MOD {nexusId} 的主文件列表：", options);
            if (pick < 0) return;
            var file = files.OrderByDescending(f => f.IsPrimary).ThenByDescending(f => f.UploadedTime).ToList()[pick];

            BusyText = "正在获取下载地址...";
            var (urls, urlError) = await AppServices.Nexus.GetDownloadUrlsAsync(int.Parse(nexusId), file.FileId);
            if (urlError != "")
            {
                await _dialogs.InfoAsync("下载链接获取失败", urlError);
                return;
            }
            if (urls.Count == 0)
            {
                await _dialogs.InfoAsync("下载链接获取失败", "NEXUS 未返回下载地址（可能需要会员账户）。");
                return;
            }

            await DownloadToItemAsync(urls[0], file.Name, targetItemPath);
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    [RelayCommand]
    public async Task RunGitHubUpdateAsync()
    {
        var repo = SelectedResult?.GitHubRepo;
        if (string.IsNullOrEmpty(repo))
        {
            await _dialogs.InfoAsync("无法更新", "此模组没有 GitHub 更新键。");
            return;
        }
        var item = await PickTargetItemAsync();
        if (item == null) return;
        await RunGitHubUpdateAsync(repo, item);
    }

    public async Task RunGitHubUpdateAsync(string repo, string targetItemPath)
    {
        IsBusy = true;
        BusyText = "正在获取 GitHub Release...";
        try
        {
            var (releases, error) = await AppServices.GitApi.GetAllReleasesAsync(repo, _settings["GithubToken"]);
            if (error != "")
            {
                await _dialogs.InfoAsync("GitHub 错误", error);
                return;
            }
            releases = releases.Where(r => !r.IsPrerelease).ToList();
            if (releases.Count == 0)
            {
                await _dialogs.InfoAsync("无发布版", "该仓库没有可用的 Release。");
                return;
            }

            var releaseOptions = releases.Select(r => $"{r.Tag} - {r.Title}").ToList();
            var rPick = await _dialogs.ChoiceAsync("选择 Release", $"{repo} 的发布版本：", releaseOptions);
            if (rPick < 0) return;
            var release = releases[rPick];

            if (release.Assets.Count == 0)
            {
                await _dialogs.InfoAsync("无附件", "该 Release 没有可下载的附件文件。");
                return;
            }
            var assetOptions = release.Assets.Select(a => a.Key).ToList();
            var aPick = await _dialogs.ChoiceAsync("选择附件", $"Release {release.Tag} 的附件：", assetOptions);
            if (aPick < 0) return;
            var (name, url) = release.Assets[aPick];

            await DownloadToItemAsync(url, name, targetItemPath);
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    [RelayCommand]
    public async Task OpenModDropPageAsync()
    {
        var id = SelectedResult?.ModDropId;
        if (string.IsNullOrEmpty(id))
        {
            await _dialogs.InfoAsync("无法打开", "此模组没有 ModDrop 更新键。");
            return;
        }
        OpenUrl($"https://www.moddrop.com/stardew-valley/mods/{id}");
    }

    [RelayCommand]
    public void OpenSuggestedUrl()
    {
        if (SelectedResult?.Url is { Length: > 0 } url)
            OpenUrl(url);
    }

    // ------------------------------------------------- 下载并新建项

    /// <summary>从 URL（NEXUS/GitHub 直链）下载压缩包并新建模组项。</summary>
    [RelayCommand]
    public async Task DownloadAndCreateItemAsync()
    {
        var sub = _settings.LastSubLibrary;
        if (string.IsNullOrEmpty(sub))
        {
            await _dialogs.InfoAsync("无法继续", "请先在「模组管理」选择数据子库。");
            return;
        }

        var url = await _dialogs.InputAsync("下载并新建模组项",
            "输入压缩包直链 URL（GitHub Release 附件等），或留空改为本地选择压缩包：", "");
        string archivePath;
        if (!string.IsNullOrWhiteSpace(url))
        {
            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(fileName)) fileName = "download.zip";
            var downloadDir = _settings.DownloadTempDirectory;
            archivePath = Path.Combine(downloadDir, fileName);
            var task = new DownloadTaskVm(fileName);
            Downloads.Add(task);
            BusyText = $"正在下载 {fileName}...";
            try
            {
                await DownloadService.DownloadFileAsync(url, archivePath,
                    (done, total) =>
                    {
                        task.Report(done, total);
                        BusyText = total > 0
                            ? $"正在下载 {fileName}... {FormatSize(done)} / {FormatSize(total)}"
                            : $"正在下载 {fileName}... {FormatSize(done)}";
                    }, task.Cancellation);
            }
            catch (OperationCanceledException)
            {
                _log.Print($"下载已取消：{fileName}", LogKind.Warning);
                return;
            }
            catch (Exception ex)
            {
                await _dialogs.InfoAsync("下载失败", ex.Message);
                return;
            }
            finally { Downloads.Remove(task); BusyText = ""; }
        }
        else
        {
            var files = await _dialogs.PickFilesAsync("选择压缩包", "压缩包", "*.zip", "*.7z", "*.rar");
            if (files == null || files.Length == 0) return;
            archivePath = files[0];
        }

        if (!ArchiveService.IsArchive(archivePath))
        {
            await _dialogs.InfoAsync("不支持的格式", "仅支持 zip / 7z / rar 压缩包。");
            return;
        }

        var itemName = Path.GetFileNameWithoutExtension(archivePath);
        var newName = await _dialogs.InputAsync("新建模组项", "输入新模组项的名称：", itemName);
        if (string.IsNullOrWhiteSpace(newName)) return;
        newName = newName.Trim();

        // 找个分类
        var cats = AppServices.Library.ScanCategories(sub);
        var catName = cats.Count > 0 ? cats[0].Key : "";
        if (cats.Count > 1)
        {
            var pick = await _dialogs.ChoiceAsync("选择分类", "新模组项放入哪个分类：", cats.Select(c => c.Key).ToList());
            if (pick < 0) return;
            catName = cats[pick].Key;
        }
        if (catName == "")
        {
            catName = "Default";
            AppServices.ItemOps.CreateCategory(sub, catName);
        }

        var itemPath = AppServices.ItemOps.CreateItem(sub, catName, newName);
        IsBusy = true;
        BusyText = "正在解压...";
        try
        {
            var extractDir = Path.Combine(_settings.DecompressTempDirectory, Guid.NewGuid().ToString("N")[..8]);
            AppServices.Archive.Extract(archivePath, extractDir);

            // 如果解压出的是单文件夹，把其内容提升到项根
            var dirs = Directory.GetDirectories(extractDir);
            if (dirs.Length == 1 && Directory.GetFiles(extractDir).Length == 0)
            {
                var inner = dirs[0];
                foreach (var d in Directory.GetDirectories(inner))
                    Directory.Move(d, Path.Combine(extractDir, Path.GetFileName(d)));
                foreach (var f in Directory.GetFiles(inner))
                    File.Move(f, Path.Combine(extractDir, Path.GetFileName(f)));
                Directory.Delete(inner, true);
            }

            // 复制到项目录
            foreach (var d in Directory.GetDirectories(extractDir))
                CopyDir(d, Path.Combine(itemPath, Path.GetFileName(d)));
            foreach (var f in Directory.GetFiles(extractDir))
                File.Copy(f, Path.Combine(itemPath, Path.GetFileName(f)), true);
            Directory.Delete(extractDir, true);

            // 自动规划：识别 manifest.json 文件夹与 Content
            foreach (var dir in Directory.GetDirectories(itemPath))
            {
                var name = Path.GetFileName(dir);
                if (name is "Screenshot" or ".config" or "_MACOSX") continue;
                if (name == "Content")
                {
                    AppendPlan(itemPath, "CD-D-CONTENT", "0");
                }
                else if (File.Exists(Path.Combine(dir, "manifest.json")))
                {
                    AppendPlan(itemPath, "CD-D-MODS", name);
                }
            }
            _log.Print($"已下载并创建模组项：{newName}（分类 {catName}），并已自动生成安装规划", LogKind.Success);
            await _dialogs.InfoAsync("完成", $"模组项 {newName} 已创建，并已自动生成安装规划。");
        }
        catch (Exception ex)
        {
            await _dialogs.InfoAsync("失败", ex.Message);
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    private static void AppendPlan(string itemPath, string key, string value)
    {
        var code2 = Path.Combine(itemPath, "Code2");
        var pairs = new List<KeyValuePair<string, string>>();
        if (File.Exists(code2))
            pairs.AddRange(SMUI.Core.IO.KeyValueFile.ReadPairs(code2));
        if (pairs.Any(p => p.Key == key && p.Value == value)) return;
        pairs.Add(new KeyValuePair<string, string>(key, value));
        SMUI.Core.IO.KeyValueFile.WritePairs(code2, pairs);
    }

    // ------------------------------------------------- 下载到项

    /// <summary>下载文件到项目录；压缩包自动解压并删除包体。</summary>
    private async Task DownloadToItemAsync(string url, string fileName, string itemPath)
    {
        var downloadDir = _settings.DownloadTempDirectory;
        var savePath = Path.Combine(downloadDir, fileName);
        var task = new DownloadTaskVm(fileName);
        Downloads.Add(task);
        BusyText = $"正在下载 {fileName}...";
        try
        {
            await DownloadService.DownloadFileAsync(url, savePath,
                (done, total) =>
                {
                    task.Report(done, total);
                    BusyText = total > 0
                        ? $"正在下载 {fileName}... {FormatSize(done)} / {FormatSize(total)}"
                        : $"正在下载 {fileName}... {FormatSize(done)}";
                }, task.Cancellation);

            if (ArchiveService.IsArchive(savePath))
            {
                BusyText = "正在解压...";
                AppServices.Archive.Extract(savePath, itemPath);
                File.Delete(savePath);
                _log.Print($"已下载并解压 {fileName} 到 {Path.GetFileName(itemPath)}", LogKind.Success);
                await _dialogs.InfoAsync("下载完成",
                    $"已下载并解压 {fileName} 到模组项。\n请到「配置队列」检查安装规划是否需要更新。");
            }
            else
            {
                var target = Path.Combine(itemPath, fileName);
                File.Copy(savePath, target, true);
                File.Delete(savePath);
                _log.Print($"已下载 {fileName} 到 {Path.GetFileName(itemPath)}", LogKind.Success);
                await _dialogs.InfoAsync("下载完成", $"已下载 {fileName} 到模组项（非压缩包，请自行处理安装方式）。");
            }
        }
        catch (OperationCanceledException)
        {
            _log.Print($"下载已取消：{fileName}", LogKind.Warning);
        }
        catch (Exception ex)
        {
            await _dialogs.InfoAsync("下载失败", ex.Message);
        }
        finally
        {
            Downloads.Remove(task);
            BusyText = "";
        }
    }

    private async Task<string?> PickTargetItemAsync()
    {
        var sub = _settings.LastSubLibrary;
        if (string.IsNullOrEmpty(sub)) return null;
        var cats = AppServices.Library.ScanCategories(sub);
        var options = new List<string>();
        var paths = new List<string>();
        foreach (var (cat, _) in cats)
        foreach (var entry in AppServices.Library.ScanItems(sub, cat))
        {
            options.Add($"[{cat}] {entry.Name}");
            paths.Add(entry.ItemPath);
        }
        if (options.Count == 0)
        {
            await _dialogs.InfoAsync("提示", "当前子库没有模组项。");
            return null;
        }
        var pick = await _dialogs.ChoiceAsync("选择目标模组项", "更新内容将下载到哪个模组项：", options);
        return pick < 0 ? null : paths[pick];
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

    internal static string FormatSize(long bytes) =>
        bytes switch
        {
            >= 1 << 30 => $"{bytes / (double)(1 << 30):F2} GB",
            >= 1 << 20 => $"{bytes / (double)(1 << 20):F1} MB",
            >= 1 << 10 => $"{bytes / (double)(1 << 10):F1} KB",
            _ => $"{bytes} B",
        };

    private static void CopyDir(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDir(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
}

/// <summary>下载队列任务行。</summary>
public partial class DownloadTaskVm : ViewModelBase
{
    private long _done;
    private long _total;
    private string _progressText = "准备中...";
    private double _progress;

    public DownloadTaskVm(string fileName) => FileName = fileName;

    public string FileName { get; }
    public CancellationTokenSource CancellationSource { get; } = new();
    public CancellationToken Cancellation => CancellationSource.Token;

    public long Done { get => _done; private set => SetProperty(ref _done, value); }
    public long Total { get => _total; private set => SetProperty(ref _total, value); }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }

    public void Report(long done, long total)
    {
        Done = done;
        Total = total;
        if (total > 0)
        {
            Progress = done * 100.0 / total;
            ProgressText = $"{UpdatesPageViewModel.FormatSize(done)} / {UpdatesPageViewModel.FormatSize(total)}";
        }
        else
        {
            ProgressText = UpdatesPageViewModel.FormatSize(done);
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Cancel() => CancellationSource.Cancel();
}
