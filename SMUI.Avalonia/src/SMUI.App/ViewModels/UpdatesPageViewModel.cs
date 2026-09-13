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

    public string NexusIdText => string.IsNullOrEmpty(NexusId) ? "-" : NexusId;
    public string SuggestedVersionText => string.IsNullOrEmpty(SuggestedVersion) ? "已是最新" : SuggestedVersion;
}

/// <summary>NEXUS 文件列表行（下载更新页右栏展示）。</summary>
public class NexusFileOptionVm
{
    public string Display { get; set; } = "";
    public string Detail { get; set; } = "";
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
        RefreshVersions();
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

    // ------------------------------------------------- 步骤一表单（复刻 WinForms：SMAPI 版本/游戏版本/操作系统平台）

    [ObservableProperty]
    private string _smapiVersion = "";

    [ObservableProperty]
    private string _gameVersion = "";

    [ObservableProperty]
    private string _osPlatform = "Windows";

    /// <summary>下载更新页左侧的 NEXUS 下载模式显示。</summary>
    [ObservableProperty]
    private string _nexusModeText = "NEXUS 下载模式：FREE";

    /// <summary>NEXUS 主文件列表（下载更新页右栏展示）。</summary>
    public ObservableCollection<NexusFileOptionVm> NexusFileOptions { get; } = new();

    public void RefreshVersions()
    {
        OsPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "Windows"
            : System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? "macOS" : "Linux";
        NexusModeText = _settings["NexusPremium"] == "True"
            ? "NEXUS 下载模式：Premium"
            : "NEXUS 下载模式：FREE";
        try
        {
            if (_settings.GamePath is { Length: > 0 } game)
            {
                var smapiDll = Path.Combine(game, "StardewModdingAPI.dll");
                if (File.Exists(smapiDll))
                    SmapiVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(smapiDll).FileVersion ?? "";
                var gameExe = Path.Combine(game, "Stardew Valley.exe");
                if (File.Exists(gameExe))
                    GameVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(gameExe).FileVersion ?? "";
            }
        }
        catch
        {
            // 版本探测失败时保留空值供手填
        }
    }

    [RelayCommand]
    private void ClearResults() => Results.Clear();

    [RelayCommand]
    private async Task RemoveSelectedResultAsync()
    {
        if (SelectedResult == null)
        {
            await _dialogs.InfoAsync("移除选中", "请先在列表中选中一个条目。");
            return;
        }
        Results.Remove(SelectedResult);
    }

    [RelayCommand]
    private async Task EditUpdateKeysAsync()
    {
        if (SelectedResult == null)
        {
            await _dialogs.InfoAsync("编辑更新键", "请先在列表中选中一个条目。");
            return;
        }
        var input = await _dialogs.InputAsync("编辑更新键",
            $"修改 {SelectedResult.Name} 的 NEXUS 模组号：", SelectedResult.NexusId ?? "");
        if (input == null) return;
        SelectedResult.NexusId = input.Trim();
        _log.Print($"已更新更新键：{SelectedResult.Name} → {input.Trim()}", LogKind.Info);
    }

    [RelayCommand]
    private async Task EditVersionAsync()
    {
        if (SelectedResult == null)
        {
            await _dialogs.InfoAsync("编辑版本", "请先在列表中选中一个条目。");
            return;
        }
        var input = await _dialogs.InputAsync("编辑版本",
            $"修改 {SelectedResult.Name} 的建议版本号：", SelectedResult.SuggestedVersion ?? "");
        if (input == null) return;
        SelectedResult.SuggestedVersion = input.Trim();
    }

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

            var orderedFiles = files.OrderByDescending(f => f.IsPrimary).ThenByDescending(f => f.UploadedTime).ToList();
            NexusFileOptions.Clear();
            foreach (var f in orderedFiles)
                NexusFileOptions.Add(new NexusFileOptionVm
                {
                    Display = f.Name,
                    Detail = $"v{f.Version} · {FormatSize(f.Size)} · ID {f.FileId}" + (f.IsPrimary ? " · 主文件" : ""),
                });
            var options = orderedFiles
                .Select(f => $"{f.Name}  (v{f.Version}, {FormatSize(f.Size)})")
                .ToList();
            var pick = await _dialogs.ChoiceAsync("选择要下载的 NEXUS 文件",
                $"NEXUS MOD {nexusId} 的主文件列表：", options);
            if (pick < 0) return;
            var file = orderedFiles[pick];

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

    /// <summary>从 URL（NEXUS/GitHub 直链）下载压缩包并新建模组项；可传入预填 URL（最新模组卡片“+”入口）。</summary>
    [RelayCommand]
    public async Task DownloadAndCreateItemAsync(string? presetUrl = null)
    {
        var sub = _settings.LastSubLibrary;
        if (string.IsNullOrEmpty(sub))
        {
            await _dialogs.InfoAsync("无法继续", "请先在「模组管理」选择数据子库。");
            return;
        }

        var url = await _dialogs.InputAsync("下载并新建模组项",
            "输入压缩包直链 URL（GitHub Release 附件等），或留空改为本地选择压缩包：", presetUrl ?? "");
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


    // ------------------------------------------------- 步骤一：检查更新表（复刻 WinForms ListView8）

    public ObservableCollection<CheckRowVm> CheckRows { get; } = new();

    [ObservableProperty]
    private CheckRowVm? _selectedCheckRow;

    [ObservableProperty]
    private int _stepIndex;

    /// <summary>把一个模组项的更新键信息加入检查更新表（管理模组右键「加入检查更新表」入口）。</summary>
    public int AddToCheckTable(string itemPath)
    {
        var info = new ItemInfo();
        info.Read(itemPath, new ItemInfo.ComputeFlags
        {
            UniqueId = true,
            UpdateKeys = true,
            InstalledVersion = true,
        }, _settings.GamePath);
        int added = 0;
        var ids = info.UniqueIds.Count > 0 ? info.UniqueIds : new List<string> { Path.GetFileName(itemPath) };
        foreach (var id in ids)
        {
            var keys = new List<string>();
            keys.AddRange(info.NexusIds.Select(n => $"nexus:{n}"));
            keys.AddRange(info.ModDropIds.Select(m => $"moddrop:{m}"));
            keys.AddRange(info.GitHubRepos.Select(g => $"github:{g}"));
            if (keys.Count == 0) keys.AddRange(info.UniqueIds.Select(u => $"smapi:{u}"));
            CheckRows.Add(new CheckRowVm
            {
                UniqueId = id,
                UpdateKeys = string.Join("|", keys),
                Version = info.InstalledVersions.FirstOrDefault() ?? "",
            });
            added++;
        }
        return added;
    }

    [RelayCommand]
    private async Task RemoveCheckRowAsync()
    {
        if (SelectedCheckRow == null) { await _dialogs.InfoAsync("移除选中", "请先在检查更新表中选中条目。"); return; }
        CheckRows.Remove(SelectedCheckRow);
    }

    [RelayCommand]
    private void ClearCheckRows() => CheckRows.Clear();

    [RelayCommand]
    private async Task EditCheckKeysAsync()
    {
        if (SelectedCheckRow == null) { await _dialogs.InfoAsync("编辑更新键", "请先在检查更新表中选中条目。"); return; }
        var s = await _dialogs.InputAsync("编辑更新键", "多个更新键用竖线隔开", SelectedCheckRow.UpdateKeys);
        if (s == null) return;
        SelectedCheckRow.UpdateKeys = s.Trim();
    }

    [RelayCommand]
    private async Task EditCheckVersionAsync()
    {
        if (SelectedCheckRow == null) { await _dialogs.InfoAsync("编辑版本", "请先在检查更新表中选中条目。"); return; }
        var s = await _dialogs.InputAsync("编辑版本", "输入该条目的版本号", SelectedCheckRow.Version);
        if (s == null) return;
        SelectedCheckRow.Version = s.Trim();
    }

    /// <summary>发送检查更新表到 smapi.io（复刻 发送并显示返回的数据）。</summary>
    [RelayCommand]
    public async Task SendCheckTableAsync()
    {
        if (CheckRows.Count == 0)
        {
            await _dialogs.InfoAsync("发送数据", "检查更新表为空：请在「管理模组」的右键菜单中选择【加入检查更新表】。");
            return;
        }
        IsBusy = true;
        BusyText = $"正在向 smapi.io 查询 {CheckRows.Count} 个条目...";
        Results.Clear();
        try
        {
            var queries = CheckRows.Select(r => new SmapiCloudService.ModQuery(
                r.UniqueId,
                r.UpdateKeys.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList(),
                r.Version)).ToList();
            var (results, error) = await AppServices.SmapiCloud.CheckModsAsync(queries);
            if (error != "")
            {
                await _dialogs.InfoAsync("检查失败", error);
                return;
            }
            foreach (var r in results)
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
            StepIndex = 1;
            _log.Print($"检查完成：{Results.Count(r => r.SuggestedVersion != "")} 个条目有更新建议", LogKind.Success);
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    [RelayCommand]
    private void SelectUpdatableResults()
    {
        // ponytail: ListBox 多选无法从 VM 驱动，此处用状态文字标记更新可用项，点击列头筛选时可见
        foreach (var r in Results)
            r.StatusText = string.IsNullOrEmpty(r.SuggestedVersion) ? "已是最新" : "有更新";
        _log.Print($"更新可用：{Results.Count(r => r.SuggestedVersion != "")} 个", LogKind.Info);
    }

    [RelayCommand]
    private void GotoStep3() => StepIndex = 2;

    [RelayCommand]
    private void ClearResultsAndBack()
    {
        Results.Clear();
        StepIndex = 0;
    }

    [RelayCommand]
    private void ClearLocalAndBack() => StepIndex = 1;

    [RelayCommand]
    private async Task ShowResultDetailAsync()
    {
        if (SelectedResult == null) { await _dialogs.InfoAsync("显示所有信息", "请先选中一个返回条目。"); return; }
        await _dialogs.InfoAsync(SelectedResult.Name,
            $"UniqueID：{SelectedResult.UniqueId}\n" +
            $"NEXUS：{(string.IsNullOrEmpty(SelectedResult.NexusId) ? "-" : SelectedResult.NexusId)}\n" +
            $"ModDrop：{(string.IsNullOrEmpty(SelectedResult.ModDropId) ? "-" : SelectedResult.ModDropId)}\n" +
            $"GitHub：{(string.IsNullOrEmpty(SelectedResult.GitHubRepo) ? "-" : SelectedResult.GitHubRepo)}\n" +
            $"建议版本：{(string.IsNullOrEmpty(SelectedResult.SuggestedVersion) ? "已是最新" : SelectedResult.SuggestedVersion)}\n" +
            $"兼容性：{SelectedResult.Compatibility}");
    }

    [RelayCommand]
    private async Task ShowLocalItemOpsAsync()
    {
        if (SelectedLocalItem == null)
        {
            await _dialogs.InfoAsync("对选中的单项操作", "请先扫描子库并选中一个模组项，再使用下方更新按钮。");
            return;
        }
        await _dialogs.InfoAsync("对选中的单项操作", $"已选中 [{SelectedLocalItem.Category}] {SelectedLocalItem.Name}。\n使用工具栏的「N网更新选中项 / GitHub 更新选中项 / 打开建议链接」执行更新。");
    }

    // ------------------------------------------------- 步骤三：在本地找到项

    public ObservableCollection<LocalItemVm> LocalItems { get; } = new();

    [ObservableProperty]
    private LocalItemVm? _selectedLocalItem;

    [RelayCommand]
    public async Task ScanLocalItemsAsync()
    {
        var sub = _settings.LastSubLibrary;
        if (string.IsNullOrEmpty(sub))
        {
            await _dialogs.InfoAsync("扫描子库", "请先在「管理模组」选择数据子库。");
            return;
        }
        LocalItems.Clear();
        var cats = AppServices.Library.ScanCategories(sub);
        await Task.Run(() =>
        {
            foreach (var (cat, _) in cats)
                foreach (var entry in AppServices.Library.ScanItems(sub, cat))
                    LocalItems.Add(new LocalItemVm { Name = entry.Name, Category = cat, ItemPath = entry.ItemPath });
        });
        _log.Print($"已扫描子库「{sub}」：{LocalItems.Count} 个模组项", LogKind.Info);
    }

    [RelayCommand]
    private void RemoveLocalItem()
    {
        if (SelectedLocalItem != null) LocalItems.Remove(SelectedLocalItem);
    }

    [RelayCommand]
    private void ClearLocalItems() => LocalItems.Clear();

    /// <summary>更新目标：优先步骤三选中的本地项。</summary>
    private async Task<string?> PickTargetItemAsync()
    {
        if (SelectedLocalItem != null) return SelectedLocalItem.ItemPath;
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

/// <summary>检查更新表行（步骤一，复刻 WinForms ListView8 三列）。</summary>
public partial class CheckRowVm : ViewModelBase
{
    public string UniqueId { get; set; } = "";
    public string UpdateKeys { get; set; } = "";
    public string Version { get; set; } = "";
}

/// <summary>步骤三本地模组项。</summary>
public class LocalItemVm
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string ItemPath { get; set; } = "";
}
