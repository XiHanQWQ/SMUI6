using System.Xml;

namespace SMUI.Core.Services;

/// <summary>
/// 自动路径检测（对应原版 设置.vb 的 选择游戏文件夹路径 / 选择数据库路径）。
/// 游戏路径：注册表（Steam/GOG）+ 全磁盘常见 Steam 库位置 + 跨平台常见路径。
/// 数据仓库：SMUI 4 / SMUI 5 旧版配置迁移。
/// </summary>
public static class PathDetector
{
    private const string GameExe = "Stardew Valley.exe";
    private const string GameExeAlt = "StardewValley.exe";

    public static bool IsValidGameFolder(string path) =>
        !string.IsNullOrEmpty(path) &&
        (File.Exists(Path.Combine(path, GameExe)) || File.Exists(Path.Combine(path, GameExeAlt)));

    /// <summary>自动检测候选游戏路径（已按存在 Stardew Valley.exe 校验）。</summary>
    public static List<string> DetectGamePaths()
    {
        var results = new List<string>();
        void Add(string? p)
        {
            if (string.IsNullOrEmpty(p)) return;
            try
            {
                var full = Path.GetFullPath(p);
                if (IsValidGameFolder(full) && !results.Contains(full, StringComparer.OrdinalIgnoreCase))
                    results.Add(full);
            }
            catch { }
        }

        if (OperatingSystem.IsWindows())
        {
            // 注册表：Steam
            Add(ReadRegistryValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 413150", "InstallLocation"));
            Add(ReadRegistryValue(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 413150", "InstallLocation"));
            // 注册表：GOG
            Add(ReadRegistryValue(@"SOFTWARE\GOG.com\Games\1453375253", "PATH"));

            // 全磁盘扫描常见 Steam 库位置（与原版一致）
            var steamSub = Path.Combine("steamapps", "common", "Stardew Valley");
            var candidates = new List<string>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                var root = drive.Name;
                candidates.Add(Path.Combine(root, "Program Files", "Steam", steamSub));
                candidates.Add(Path.Combine(root, "Program Files (x86)", "Steam", steamSub));
                candidates.Add(Path.Combine(root, "SteamLibrary", steamSub));
                candidates.Add(Path.Combine(root, "Program Files", "ModifiableWindowsApps", "Stardew Valley"));
                // 常见自定义 Steam 库根
                candidates.Add(Path.Combine(root, "Steam", steamSub));
                candidates.Add(Path.Combine(root, "Games", "Steam", steamSub));
            }
            // 用户级 Steam 库
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            candidates.Add(Path.Combine(programFilesX86, "Steam", steamSub));
            foreach (var c in candidates) Add(c);
        }
        else if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Add(Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common", "Stardew Valley"));
        }
        else if (OperatingSystem.IsLinux())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Add(Path.Combine(home, ".steam", "steam", "steamapps", "common", "Stardew Valley"));
            Add(Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "Stardew Valley"));
            Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam", "steamapps", "common", "Stardew Valley"));
        }

        return results;
    }

    private static string? ReadRegistryValue(string keyPath, string valueName)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
            {
                using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(keyPath);
                var value = key?.GetValue(valueName) as string;
                if (!string.IsNullOrEmpty(value) && Directory.Exists(value)) return value;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 检测旧版 SMUI 4 / SMUI 5 的模组数据仓库路径（迁移用）。
    /// 返回 (路径, 来源描述) 列表。
    /// </summary>
    public static List<(string Path, string Source)> DetectLegacyRepositoryPaths()
    {
        var results = new List<(string, string)>();

        // SMUI Client 4
        try
        {
            var p4 = Path.Combine(@"C:\Users\Public\1059 Studio\SMUI Client 4\Settings\UserSettings.xml");
            if (File.Exists(p4))
            {
                var doc = new XmlDocument();
                doc.Load(p4);
                var node = doc.SelectSingleNode("Data/LibraryPath");
                var path = node?.InnerText;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    results.Add((path, "从 SMUI 4 迁移"));
            }
        }
        catch { }

        // SMUI Client 5
        try
        {
            var p5 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "1059 Studio", "SMUI Client 5 Cache", "Settings.xml");
            if (File.Exists(p5))
            {
                var doc = new XmlDocument();
                doc.Load(p5);
                var node = doc.SelectSingleNode("data/ModRepositoryPath");
                var path = node?.InnerText;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    results.Add((path, "从 SMUI 5 迁移"));
            }
        }
        catch { }

        return results;
    }
}
