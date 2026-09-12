using SMUI.Core.IO;
using SMUI.Core.Models;

namespace SMUI.Core.Services;

/// <summary>分类/项的外观标记。</summary>
public class AppearanceMark
{
    /// <summary>RED/ORANGE/YELLOW/GREEN/AQUA/BLUE/PURPLE 或空。</summary>
    public string Color { get; set; } = "";
    /// <summary>BD 粗体 / LC 斜体 / UL 下划线 / SO 删除线 或空。</summary>
    public string FontStyle { get; set; } = "";
}

/// <summary>模组项列表条目（UI 显示用的完整信息）。</summary>
public class ModItemEntry
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string ItemPath { get; set; } = "";
    /// <summary>仓库版本（可能带 ◀/▶ 指示）。</summary>
    public string VersionText { get; set; } = "";
    /// <summary>仓库原始版本号。</summary>
    public string Version { get; set; } = "";
    /// <summary>游戏内已安装版本号。</summary>
    public string InstalledVersion { get; set; } = "";
    /// <summary>安装状态 Key。</summary>
    public string Status { get; set; } = InstallStatus.UnKnow;
    /// <summary>附加状态文本（"更新可用"/"已有新的"）。</summary>
    public string ExtraStatus { get; set; } = "";
    public AppearanceMark Appearance { get; set; } = new();
}

/// <summary>
/// 模组库（数据仓库）访问服务：子库/分类/项扫描、排序持久化、旧版 Code 自动转换。
/// 目录结构：仓库\子库\分类\项\（Code2、Version、Color、Font、README(.rtf)、Screenshot\、内容包...）
/// </summary>
public class LibraryService
{
    private readonly SettingsService _settings;

    public LibraryService(SettingsService settings)
    {
        _settings = settings;
    }

    // ------------------------------------------------------------- 子库

    /// <summary>扫描所有数据子库（跳过 . 开头的文件夹）。</summary>
    public List<string> ScanSubLibraries()
    {
        var repo = _settings.RepositoryPath;
        if (string.IsNullOrEmpty(repo) || !Directory.Exists(repo)) return new List<string>();
        return Directory.GetDirectories(repo)
            .Select(Path.GetFileName)!
            .Where(n => !n.StartsWith("."))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string SubLibraryPath(string subLibrary) => Path.Combine(_settings.RepositoryPath, subLibrary);

    public string CreateSubLibrary(string name)
    {
        var path = Path.Combine(_settings.RepositoryPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    // ------------------------------------------------------------- 分类

    public string CategoryPath(string subLibrary, string category) => Path.Combine(SubLibraryPath(subLibrary), category);

    /// <summary>扫描分类（带 SORT 排序持久化），返回 (分类名, 外观标记) 列表。</summary>
    public List<KeyValuePair<string, AppearanceMark>> ScanCategories(string subLibrary)
    {
        var result = new List<KeyValuePair<string, AppearanceMark>>();
        if (string.IsNullOrEmpty(subLibrary)) return result;
        var subPath = SubLibraryPath(subLibrary);
        if (!Directory.Exists(subPath)) return result;

        var folders = Directory.GetDirectories(subPath).Select(Path.GetFileName)!.ToList();
        var sortFile = Path.Combine(subPath, "SORT");
        var sort = ReadLinesIfExists(sortFile);

        // 先按 SORT 顺序，再追加新发现的
        var ordered = new List<string>();
        foreach (var name in sort)
        {
            if (folders.Contains(name))
            {
                ordered.Add(name);
                folders.Remove(name);
            }
        }
        ordered.AddRange(folders);
        if (ordered.Count != sort.Count) WriteLines(sortFile, ordered);

        foreach (var name in ordered)
        {
            var catPath = Path.Combine(subPath, name);
            result.Add(new KeyValuePair<string, AppearanceMark>(name, ReadAppearance(catPath)));
        }
        return result;
    }

    // ------------------------------------------------------------- 模组项

    /// <summary>扫描分类下的模组项（带 SORT 排序持久化），并刷新每项的版本与安装状态。</summary>
    public List<ModItemEntry> ScanItems(string subLibrary, string category, bool forceStatus = true)
    {
        var result = new List<ModItemEntry>();
        var catPath = CategoryPath(subLibrary, category);
        if (!Directory.Exists(catPath)) return result;

        var folders = Directory.GetDirectories(catPath).Select(Path.GetFileName)!.ToList();
        var sortFile = Path.Combine(catPath, "SORT");
        var sort = ReadLinesIfExists(sortFile);

        var ordered = new List<string>();
        foreach (var name in sort)
        {
            if (folders.Contains(name))
            {
                ordered.Add(name);
                folders.Remove(name);
            }
        }
        ordered.AddRange(folders);
        if (ordered.Count != sort.Count) WriteLines(sortFile, ordered);

        foreach (var name in ordered)
        {
            var entry = BuildItemEntry(subLibrary, category, name);
            if (entry != null) result.Add(entry);
        }
        return result;
    }

    /// <summary>为单个模组项构建显示信息（自动转换旧 Code → Code2）。</summary>
    public ModItemEntry? BuildItemEntry(string subLibrary, string category, string itemName)
    {
        var itemPath = Path.Combine(SubLibraryPath(subLibrary), category, itemName);
        if (!Directory.Exists(itemPath)) return null;

        var entry = new ModItemEntry
        {
            Name = itemName,
            Category = category,
            ItemPath = itemPath,
            Appearance = ReadAppearance(itemPath),
        };

        // 旧版安装命令文件自动转换
        var code2 = Path.Combine(itemPath, "Code2");
        if (!File.Exists(code2))
        {
            var code = Path.Combine(itemPath, "Code");
            if (File.Exists(code))
                KeyValueFile.WritePairs(code2, IO.KeyValueFile.ParseText(ConvertLegacyCommands(File.ReadAllText(code))));
        }

        var info = new ItemInfo();
        info.Read(itemPath, ItemInfo.ComputeFlags.StatusAndVersion, _settings.GamePath);
        if (info.ErrorMessage != "")
        {
            entry.Status = InstallStatus.NoConfigured;
            entry.VersionText = "未知版本";
            return entry;
        }

        entry.Version = info.Versions.Count > 0 ? info.Versions[0] : "";
        entry.InstalledVersion = info.InstalledVersions.Count > 0 ? info.InstalledVersions[0] : "";
        entry.Status = info.Status;

        if (entry.Version != "" && entry.InstalledVersion != "")
        {
            if (entry.Version != entry.InstalledVersion && entry.Status != InstallStatus.Incomplete)
            {
                var cmp = Util.VersionHelper.Compare(entry.Version, entry.InstalledVersion);
                if (cmp > 0)
                {
                    entry.VersionText = $"{entry.Version} ◀ {entry.InstalledVersion}";
                    entry.ExtraStatus = "更新可用";
                }
                else
                {
                    entry.VersionText = $"{entry.Version} ▶ {entry.InstalledVersion}";
                    entry.ExtraStatus = "已有新的";
                }
            }
            else
            {
                entry.VersionText = entry.Version;
            }
        }
        else if (entry.Version != "")
        {
            entry.VersionText = entry.Version;
        }
        else if (File.Exists(Path.Combine(itemPath, "Version")))
        {
            entry.VersionText = File.ReadAllText(Path.Combine(itemPath, "Version")).Trim();
        }
        else
        {
            entry.VersionText = "未知版本";
        }
        return entry;
    }

    public AppearanceMark ReadAppearance(string folder)
    {
        var mark = new AppearanceMark();
        var colorFile = Path.Combine(folder, "Color");
        if (File.Exists(colorFile)) mark.Color = File.ReadAllText(colorFile).Trim();
        var fontFile = Path.Combine(folder, "Font");
        if (File.Exists(fontFile)) mark.FontStyle = File.ReadAllText(fontFile).Trim();
        return mark;
    }

    // ------------------------------------------------------------- 描述与预览图

    public enum DescriptionSource { Rtf, Txt, Json, None }

    public (string text, DescriptionSource source) ReadDescription(string itemPath, ItemInfo? cachedInfo = null)
    {
        var rtf = Path.Combine(itemPath, "README.rtf");
        if (File.Exists(rtf)) return (rtf, DescriptionSource.Rtf);
        var txt = Path.Combine(itemPath, "README");
        if (File.Exists(txt)) return (File.ReadAllText(txt), DescriptionSource.Txt);

        var info = cachedInfo;
        if (info == null)
        {
            info = new ItemInfo();
            info.Read(itemPath, new ItemInfo.ComputeFlags { Description = true }, _settings.GamePath);
        }
        if (info.Descriptions.Count > 0)
            return (string.Join("\r\n\r\n", info.Descriptions), DescriptionSource.Json);
        return ("", DescriptionSource.None);
    }

    public List<string> ListScreenshots(string itemPath)
    {
        var dir = Path.Combine(itemPath, "Screenshot");
        if (!Directory.Exists(dir)) return new List<string>();
        return Directory.GetFiles(dir)
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ------------------------------------------------------------- 工具

    private static List<string> ReadLinesIfExists(string path)
    {
        if (!File.Exists(path)) return new List<string>();
        return File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l != "").ToList();
    }

    private static void WriteLines(string path, List<string> lines)
    {
        if (!Directory.Exists(Path.GetDirectoryName(path)!)) return;
        File.WriteAllText(path, string.Join("\r\n", lines));
    }

    /// <summary>保存分类排序。</summary>
    public void SaveSort(string folderPath, IReadOnlyList<string> order)
    {
        if (!Directory.Exists(folderPath)) return;
        File.WriteAllText(Path.Combine(folderPath, "SORT"), string.Join("\r\n", order));
    }

    /// <summary>把旧版安装命令文本转换为 Code2 安装规划（对应原版 命令规划转换）。</summary>
    public static string ConvertLegacyCommands(string commandText)
    {
        var lines = commandText.Replace("\r\n", "\n").Split('\n').ToList();
        var result = new List<string>();
        var coreClass = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            switch (lines[i])
            {
                case "CDCD":
                case "CDCP":
                    if (i == lines.Count - 1) goto done;
                    result.Add("CD-D-MODS=" + lines[++i]);
                    break;
                case "CDMAD":
                    if (i == lines.Count - 1) goto done;
                    result.Add("CD-D-MODS-COVER=" + lines[++i]);
                    break;
                case "CDGCD":
                    if (i >= lines.Count - 2) goto done;
                    result.Add($"CD-D-ROOT={lines[++i]}|{lines[++i]}");
                    break;
                case "CDCC":
                case "CDVD":
                    result.Add("CD-D-CONTENT=0");
                    break;
                case "CDGCF":
                    if (i >= lines.Count - 2) goto done;
                    result.Add($"CD-F=False|True|False|{lines[++i]}|{lines[++i]}");
                    break;
                case "CDGCF-SHA":
                    if (i >= lines.Count - 2) goto done;
                    result.Add($"CD-F=False|True|True|{lines[++i]}|{lines[++i]}");
                    break;
                case "CDGRF":
                    if (i >= lines.Count - 2) goto done;
                    result.Add($"CD-F=True|True|True|{lines[++i]}|{lines[++i]}");
                    break;
                case "CDF":
                    if (i >= lines.Count - 2) goto done;
                    result.Add($"CD-F=True|False|False|{lines[++i]}|{lines[++i]}");
                    break;
                case "RQ-D-IN":
                    if (i == lines.Count - 1) goto done;
                    result.Add($"CR-Check-EXIST=Install|Folder|True|{lines[++i]}");
                    break;
                case "RQ-D-UN":
                    if (i == lines.Count - 1) goto done;
                    result.Add($"CR-Check-EXIST=UnInstall|Folder|True|{lines[++i]}");
                    break;
                case "RQ-F-IN":
                    if (i == lines.Count - 1) goto done;
                    result.Add($"CR-Check-EXIST=Install|File|True|{lines[++i]}");
                    break;
                case "RQ-F-UN":
                    if (i == lines.Count - 1) goto done;
                    result.Add($"CR-Check-EXIST=UnInstall|File|True|{lines[++i]}");
                    break;
                case "CR-UN-OFF":
                    result.Add("CR-UN=ERROR");
                    break;
                case "CR-UN-CANCEL":
                    result.Add("CR-UN=CANCEL");
                    break;
                case "CR-CG-DB":
                    coreClass.Add("CG-DB");
                    break;
                case "CR-CDS-CDCD-AMD":
                    coreClass.Add("Mods-AMD");
                    break;
                case "CR-FILE-ALLOW-ALL":
                    coreClass.Add("FILE-ALLOW-ALL");
                    break;
            }
        }
    done:
        var body = string.Join("\r\n", result);
        return coreClass.Count > 0 ? "CORE-CLASS=" + string.Join("|", coreClass) + "\r\n" + body : body;
    }
}
