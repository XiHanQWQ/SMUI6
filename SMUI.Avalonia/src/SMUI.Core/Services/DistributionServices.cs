using System.Text.Json;
using SMUI.Core.IO;
using SMUI.Core.Models;

namespace SMUI.Core.Services;

/// <summary>扫描到的游戏内模组。</summary>
public record GameModInfo(string FolderName, string ModName, string Version, string UniqueId, string FullPath);

/// <summary>
/// 全局模组安装检查：扫描游戏 Mods 目录，把手动安装的模组一键导入数据库为模组项
/// （对应原版 集成工具 → 全局模组安装检查）。
/// </summary>
public static class GameModsImporter
{
    /// <summary>扫描游戏 Mods 下所有含 manifest.json 的文件夹。</summary>
    public static List<GameModInfo> ScanGameMods(string gamePath)
    {
        var result = new List<GameModInfo>();
        var mods = Path.Combine(gamePath, "Mods");
        if (!Directory.Exists(mods)) return result;

        foreach (var dir in Directory.GetDirectories(mods))
        {
            var manifest = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifest)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                var root = doc.RootElement;
                result.Add(new GameModInfo(
                    Path.GetFileName(dir),
                    GetStr(root, "Name") is { Length: > 0 } n ? n : Path.GetFileName(dir),
                    GetStr(root, "Version"),
                    GetStr(root, "UniqueID"),
                    dir));
            }
            catch
            {
                result.Add(new GameModInfo(Path.GetFileName(dir), Path.GetFileName(dir), "", "", dir));
            }
        }
        return result;
    }

    /// <summary>
    /// 把游戏内模组文件夹导入为模组项：复制文件夹入库 + 写入 CD-D-MODS 安装规划 + 保存已装版本。
    /// 返回项路径。
    /// </summary>
    public static string ImportAsItem(GameModInfo mod, string subLibrary, string category,
        string repositoryPath, string? itemName = null)
    {
        var name = string.IsNullOrWhiteSpace(itemName) ? mod.FolderName : itemName.Trim();
        var catPath = Path.Combine(repositoryPath, subLibrary, category);
        Directory.CreateDirectory(catPath);
        var itemPath = Path.Combine(catPath, name);
        if (Directory.Exists(itemPath))
            throw new IOException($"目标文件夹已存在：{name}");

        Directory.CreateDirectory(itemPath);
        CopyDirectory(mod.FullPath, itemPath);

        // 写入安装规划
        KeyValueFile.WritePairs(Path.Combine(itemPath, "Code2"),
            new List<KeyValuePair<string, string>>
            {
                new("CD-D-MODS", mod.FolderName),
            });

        // 保存当前已装版本号（以便安装状态识别为已装同版本）
        if (!string.IsNullOrEmpty(mod.Version))
            File.WriteAllText(Path.Combine(itemPath, "Version"), mod.Version);

        return itemPath;
    }

    private static string? GetStr(JsonElement el, string prop)
    {
        foreach (var p in el.EnumerateObject())
            if (p.Name.Equals(prop, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                return p.Value.GetString();
        return null;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
}

/// <summary>单条下载记录。</summary>
public class PresetEntry
{
    public string ItemPath { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>
/// 批量分发 / 导出预设（对应原版 DLC5 批量分发管理，数据存于 UserData/Presets.json）。
/// </summary>
public class DistributionPresetService
{
    private readonly SettingsService _settings;
    private readonly List<PresetEntry> _entries = new();

    public DistributionPresetService(SettingsService settings)
    {
        _settings = settings;
        Load();
    }

    public IReadOnlyList<PresetEntry> Entries => _entries;
    public int Count => _entries.Count;

    private string FilePath => _settings.DistributionPresetFile;

    public void Load()
    {
        _entries.Clear();
        try
        {
            if (!File.Exists(FilePath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (doc.RootElement.TryGetProperty("Items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    if (el.TryGetProperty("ItemPath", out var p))
                        _entries.Add(new PresetEntry
                        {
                            ItemPath = p.GetString() ?? "",
                            Name = el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
                        });
                }
            }
        }
        catch { }
    }

    public void Save()
    {
        var payload = new Dictionary<string, object?>
        {
            ["Items"] = _entries.Select(e => new Dictionary<string, object?>
            {
                ["ItemPath"] = e.ItemPath,
                ["Name"] = e.Name,
            }).ToList(),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>加入预设（按路径去重）。</summary>
    public int Add(IEnumerable<string> itemPaths)
    {
        var added = 0;
        foreach (var path in itemPaths)
        {
            if (_entries.Any(e => e.ItemPath.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
            _entries.Add(new PresetEntry { ItemPath = path, Name = Path.GetFileName(path) });
            added++;
        }
        Save();
        return added;
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    /// <summary>把预设中的全部模组项批量导出为 .smuimpak 到目标目录。</summary>
    public (int ok, int fail, string outputDir) ExportAll(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var ok = 0;
        var fail = 0;
        var archive = new ArchiveService();
        foreach (var entry in _entries)
        {
            try
            {
                if (!Directory.Exists(entry.ItemPath)) { fail++; continue; }
                archive.PackDirectory(entry.ItemPath, Path.Combine(outputDir, entry.Name + ".smuimpak"));
                ok++;
            }
            catch
            {
                fail++;
            }
        }
        return (ok, fail, outputDir);
    }
}
