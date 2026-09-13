using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmuiCore;
using SmuiCore.GitApi;
using SMUI.Core.Services;

namespace SMUI.App.ViewModels;

/// <summary>集成工具页（复刻 WinForms 集成工具二级菜单：SMAPI 安装管理器 / 全局模组安装检查 / 存档编辑器 / 批量分发管理）。</summary>
public partial class ToolsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;

    public ToolsViewModel(SettingsService settings)
    {
        _settings = settings;
        RefreshDownloaded();
    }

    /// <summary>GitHub 发行版文件列表（对应 WinForms UiComboBox7）。</summary>
    public ObservableCollection<string> AvailableFiles { get; } = new();

    /// <summary>已下载的文件（对应 WinForms UiComboBox9）。</summary>
    public ObservableCollection<string> DownloadedFiles { get; } = new();

    /// <summary>集成工具二级菜单选中索引（SMAPI 安装管理器 / 全局模组安装检查 / 存档编辑器 / 批量分发管理）。</summary>
    [ObservableProperty] private int _toolIndex;

    [ObservableProperty] private int _availableIndex = -1;
    [ObservableProperty] private int _downloadedIndex = -1;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private int _downloadPercent;
    [ObservableProperty] private bool _isDownloading;

    private DownloadState? _downloadState;

    private string DownloadDir => _settings.SmapiDownloadDirectory;
    private string DecompressDir => _settings.SmapiDecompressDirectory;

    /// <summary>刷新文件：获取 Pathoschild/SMAPI 的 GitHub 发行版附件列表。</summary>
    [RelayCommand]
    private async Task RefreshFilesAsync()
    {
        if (StatusText == "正在获取文件列表") return;
        StatusText = "正在获取文件列表";
        var reader = new GitHubAllReleaseFile();
        var error = await Task.Run(() => reader.获取("Pathoschild/SMAPI"));
        if (error != "")
        {
            StatusText = "获取失败";
            return;
        }
        AvailableFiles.Clear();
        _availableUrlList.Clear();
        foreach (var release in reader.发行版数据集合)
            foreach (var file in release.可供下载的文件)
            {
                _availableUrlList.Add(file);
                AvailableFiles.Add(file.Key);
            }
        // 与 WinForms 一致：首项为 dev 版时跳过
        AvailableIndex = AvailableFiles.Count > 1 && AvailableFiles[0].Contains("dev") ? 1 :
                         AvailableFiles.Count > 0 ? 0 : -1;
        StatusText = "获取成功";
    }

    private readonly List<KeyValuePair<string, string>> _availableUrlList = new();

    /// <summary>下载选择的文件（带进度与取消）。</summary>
    [RelayCommand]
    private async Task DownloadSelectedAsync()
    {
        if (IsDownloading) return;
        if (AvailableIndex < 0 || AvailableIndex >= _availableUrlList.Count)
        {
            StatusText = "请先刷新并选择要下载的文件";
            return;
        }
        var (name, url) = (_availableUrlList[AvailableIndex].Key, _availableUrlList[AvailableIndex].Value);
        var target = Path.Combine(DownloadDir, name);
        var state = new DownloadState();
        _downloadState = state;
        IsDownloading = true;
        DownloadPercent = 0;
        StatusText = "正在下载";
        var downloadTask = Task.Run(() => Downloader.DownloadFile(url, target, state));
        while (!downloadTask.IsCompleted)
        {
            DownloadPercent = state.总字节量 > 0 ? (int)(state.已下载字节量 * 100 / state.总字节量) : 0;
            await Task.Delay(200);
        }
        var error = await downloadTask;
        IsDownloading = false;
        DownloadPercent = 100;
        StatusText = error == "" ? "下载完成" : $"下载失败：{error}";
        RefreshDownloaded();
    }

    /// <summary>取消下载。</summary>
    [RelayCommand]
    private void CancelDownload()
    {
        if (_downloadState != null) _downloadState.是否终止下载 = true;
    }

    /// <summary>刷新已下载的文件列表。</summary>
    [RelayCommand]
    private void RefreshDownloaded()
    {
        DownloadedFiles.Clear();
        try
        {
            if (Directory.Exists(DownloadDir))
                foreach (var f in new DirectoryInfo(DownloadDir).GetFiles("*.*"))
                    DownloadedFiles.Add(f.Name);
        }
        catch { }
        DownloadedIndex = DownloadedFiles.Count > 0 ? 0 : -1;
    }

    /// <summary>运行安装：解压所选压缩包并启动 SMAPI.Installer.exe --install。</summary>
    [RelayCommand]
    private async Task InstallAsync() => await RunInstallerAsync(true);

    /// <summary>运行卸载：解压所选压缩包并启动 SMAPI.Installer.exe --uninstall。</summary>
    [RelayCommand]
    private async Task UninstallAsync() => await RunInstallerAsync(false);

    private async Task RunInstallerAsync(bool install)
    {
        if (DownloadedIndex < 0 || DownloadedIndex >= DownloadedFiles.Count)
        {
            StatusText = "请先下载 SMAPI 安装包";
            return;
        }
        var archivePath = Path.Combine(DownloadDir, DownloadedFiles[DownloadedIndex]);
        if (!File.Exists(archivePath)) { StatusText = "未找到安装包文件"; return; }

        StatusText = "正在解压";
        ClearDecompressCore();
        string? installer = null;
        try
        {
            installer = await Task.Run(() =>
            {
                new ArchiveService().Extract(archivePath, DecompressDir);
                return Directory.EnumerateFiles(DecompressDir, "SMAPI.Installer.exe", SearchOption.AllDirectories)
                                .FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            StatusText = $"解压失败：{ex.Message}";
            return;
        }
        if (installer == null)
        {
            StatusText = "压缩包中未找到 SMAPI.Installer.exe";
            return;
        }

        StatusText = install ? "正在运行安装" : "正在运行卸载";
        var args = $"--no-prompt {(install ? "--install" : "--uninstall")} --game-path \"{_settings.GamePath}\"";
        try
        {
            var p1 = Process.Start(new ProcessStartInfo(installer, args) { UseShellExecute = false });
            if (p1 != null) await p1.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"启动安装器失败：{ex.Message}";
            return;
        }
        ClearDecompressCore();

        var smapiExe = Path.Combine(_settings.GamePath, "StardewModdingAPI.exe");
        if (install)
        {
            if (File.Exists(smapiExe))
            {
                var vi = FileVersionInfo.GetVersionInfo(smapiExe);
                StatusText = $"已安装：SMAPI {vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}";
            }
            else
                StatusText = "安装失败，未检测到目标位置的 SMAPI 程序文件";
        }
        else
            StatusText = File.Exists(smapiExe) ? "卸载失败，目标位置仍然存在 SMAPI 程序文件" : "卸载完成";
    }

    /// <summary>清理下载目录。</summary>
    [RelayCommand]
    private void ClearDownload()
    {
        ClearDirectory(DownloadDir);
        RefreshDownloaded();
        StatusText = "已清空下载目录";
    }

    /// <summary>清理解压目录。</summary>
    [RelayCommand]
    private void ClearDecompress()
    {
        ClearDecompressCore();
        StatusText = "已清空解压目录";
    }

    private void ClearDecompressCore() => ClearDirectory(DecompressDir);

    private static void ClearDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in new DirectoryInfo(dir).GetFiles("*.*", SearchOption.AllDirectories)) f.Delete();
            foreach (var d in new DirectoryInfo(dir).GetDirectories("*", SearchOption.AllDirectories))
                d.Delete(true);
        }
        catch { }
    }
}
