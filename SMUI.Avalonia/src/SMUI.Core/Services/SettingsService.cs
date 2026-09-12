using SMUI.Core.IO;

namespace SMUI.Core.Services;

/// <summary>
/// 全局设置（键值对存储于 UserData\Settings，键名与 SMUI 6 完全兼容）。
/// </summary>
public class SettingsService
{
    private readonly Dictionary<string, string> _data = new();
    private readonly string _filePath;

    public static readonly string[] DefaultKeys =
    {
        "StardewValleyGamePath", "LocalRepositoryPath", "StardewValleyGameBackupPath",
        "VisualStudioCodeEXE", "VisualStudioEXE",
        "DisplayLanguage", "NewsLanguage", "NewsSever", "UpdateSever", "AlternativeUpdateSever",
        "NexusAPI", "GiteeToken", "GithubToken",
        "LaunchSelection", "LaunchParameters", "CustomLaunchCMD",
        "SaveUserWindowSize", "MainWindowWidth", "MainWindowHeight",
        "AutoGetNews", "SaveNewsInTodayUse", "AutoCheckUpdate", "AutoStartDownloadUpdate",
        "AutoSelectFirstNexusDownloadSever", "AutoConvertWebpToPng", "FontName",
        "UploadUserInfo", "UploadWindowsVer", "UploadCPU0", "UploadRAM", "UploadCDiskCapacity", "UploadGPU", "UploadScreen",
        "AgreementSigned", "DateOfGetNews", "ProcessMonitor", "PerformanceMonitor",
        "LastUsedSubLibraryName", "UseStandaloneWebView2", "WebView2StandalonePath",
        "AdditionalBuiltinParameters", "DownloadFileUseSMUI5Color", "DownloadFileUseBigBuffer",
        "NexusPremium", "BGP_News", "BGP_Category", "BGP_ModItem", "WelcomeWindow",
    };

    public SettingsService(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        _filePath = Path.Combine(BaseDirectory, "UserData", "Settings");
    }

    public string BaseDirectory { get; }

    public string UserDataDirectory => Path.Combine(BaseDirectory, "UserData");
    public string LanguageDirectory => Path.Combine(UserDataDirectory, "Language");
    public string SmapiDownloadDirectory => Path.Combine(UserDataDirectory, "SmapiDownload");
    public string SmapiDecompressDirectory => Path.Combine(UserDataDirectory, "SmapiDecompress");
    public string TodayNewsFile => Path.Combine(UserDataDirectory, "TodayNews");
    public string DataBaseCsvFile => Path.Combine(UserDataDirectory, "ModsCoordinationDataBase.csv");
    public string DistributionPresetFile => Path.Combine(UserDataDirectory, "Presets.json");

    public string this[string key]
    {
        get => _data.TryGetValue(key, out var v) ? v : "";
        set => _data[key] = value;
    }

    public bool GetBool(string key) => _data.TryGetValue(key, out var v) && v == "True";
    public void SetBool(string key, bool value) => _data[key] = value ? "True" : "False";

    public string GamePath { get => this["StardewValleyGamePath"]; set => this["StardewValleyGamePath"] = value; }
    public string RepositoryPath { get => this["LocalRepositoryPath"]; set => this["LocalRepositoryPath"] = value; }
    public string GameBackupPath { get => this["StardewValleyGameBackupPath"]; set => this["StardewValleyGameBackupPath"] = value; }
    public string LastSubLibrary { get => this["LastUsedSubLibraryName"]; set => this["LastUsedSubLibraryName"] = value; }

    /// <summary>加载设置；文件不存在时写入默认值（协议界面已按需求移除，AgreementSigned 恒为 True）。</summary>
    public void Load()
    {
        foreach (var key in DefaultKeys) _data[key] = "";
        _data["AlternativeUpdateSever"] = "No Use";
        _data["LaunchSelection"] = "1";
        _data["SaveUserWindowSize"] = "True";
        _data["MainWindowWidth"] = "1280";
        _data["MainWindowHeight"] = "720";
        _data["AutoGetNews"] = "True";
        _data["AutoCheckUpdate"] = "True";
        _data["AutoStartDownloadUpdate"] = "True";
        _data["AutoConvertWebpToPng"] = "True";
        _data["UploadUserInfo"] = "True";
        _data["FontName"] = "Microsoft YaHei UI";
        _data["AgreementSigned"] = "True";

        EnsureUserDirectories();

        if (File.Exists(_filePath))
        {
            try
            {
                foreach (var (key, value) in KeyValueFile.ReadPairs(_filePath))
                    _data[key] = value;
                // 需求变更：不再显示许可协议，强制视为已签署
                _data["AgreementSigned"] = "True";
            }
            catch
            {
                // 读取失败时使用默认值
            }
        }
        else
        {
            Save();
        }
    }

    public void Save()
    {
        EnsureUserDirectories();
        KeyValueFile.WriteDictionary(_filePath, _data);
    }

    public void EnsureUserDirectories()
    {
        Directory.CreateDirectory(UserDataDirectory);
        Directory.CreateDirectory(LanguageDirectory);
        Directory.CreateDirectory(SmapiDownloadDirectory);
        Directory.CreateDirectory(SmapiDecompressDirectory);
    }

    /// <summary>数据库内的下载/解压临时目录。</summary>
    public string DownloadTempDirectory
    {
        get
        {
            var p = Path.Combine(RepositoryPath, ".Download");
            Directory.CreateDirectory(p);
            return p;
        }
    }

    public string DecompressTempDirectory
    {
        get
        {
            var p = Path.Combine(RepositoryPath, ".Decompress");
            Directory.CreateDirectory(p);
            return p;
        }
    }
}
