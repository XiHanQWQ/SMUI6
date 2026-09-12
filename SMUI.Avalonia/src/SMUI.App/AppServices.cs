using SMUI.App.Services;
using SMUI.Core.Services;

namespace SMUI.App;

/// <summary>应用级服务定位器（轻量 DI）。</summary>
public static class AppServices
{
    public static SettingsService Settings { get; private set; } = null!;
    public static LibraryService Library { get; private set; } = null!;
    public static ModItemOperations ItemOps { get; private set; } = null!;
    public static ArchiveService Archive { get; private set; } = null!;
    public static GitApiService GitApi { get; private set; } = null!;
    public static DownloadService Downloader { get; private set; } = null!;
    public static NexusApiService Nexus { get; private set; } = null!;
    public static SmapiCloudService SmapiCloud { get; private set; } = null!;
    public static DistributionPresetService Presets { get; private set; } = null!;
    public static UiLogService Log { get; private set; } = null!;
    public static DialogService Dialogs { get; private set; } = null!;


    public static void Initialize(Func<Avalonia.Controls.Window?> getWindow)
    {
        Settings = new SettingsService();
        Settings.Load();
        Library = new LibraryService(Settings);
        ItemOps = new ModItemOperations(Settings);
        Archive = new ArchiveService();
        GitApi = new GitApiService();
        Downloader = new DownloadService();
        Nexus = new NexusApiService { ApiKey = Settings["NexusAPI"] };
        SmapiCloud = new SmapiCloudService();
        Presets = new DistributionPresetService(Settings);
        Log = new UiLogService();
        Dialogs = new DialogService(getWindow);
    }

    public static void SaveSettings() => Settings.Save();
}
