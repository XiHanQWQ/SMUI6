using SMUI.Core.Models;

namespace SMUI.Core.Services;

/// <summary>模组项与分类的文件操作（新建/重命名/移动/删除/颜色字体标记）。</summary>
public class ModItemOperations
{
    private readonly SettingsService _settings;

    public ModItemOperations(SettingsService settings)
    {
        _settings = settings;
    }

    private string SubPath(string subLibrary) => Path.Combine(_settings.RepositoryPath, subLibrary);
    private string CatPath(string subLibrary, string category) => Path.Combine(SubPath(subLibrary), category);
    private string ItemPath(string subLibrary, string category, string item) => Path.Combine(CatPath(subLibrary, category), item);

    // ------------------------------------------------------------- 分类

    public void CreateCategory(string subLibrary, string name)
    {
        Directory.CreateDirectory(CatPath(subLibrary, name));
    }

    public void RenameCategory(string subLibrary, string oldName, string newName)
    {
        if (Directory.Exists(CatPath(subLibrary, newName)))
            throw new IOException("目标文件夹已存在：" + newName);
        Directory.Move(CatPath(subLibrary, oldName), CatPath(subLibrary, newName));
    }

    public void MoveCategory(string subLibrary, string name, string targetSubLibrary)
    {
        var target = CatPath(targetSubLibrary, name);
        if (Directory.Exists(target))
            throw new IOException("目标子库中已存在同名分类：" + name);
        Directory.Move(CatPath(subLibrary, name), target);
    }

    public void DeleteCategory(string subLibrary, string name)
    {
        var path = CatPath(subLibrary, name);
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    public void SetCategoryColor(string subLibrary, string category, string colorKey)
    {
        var file = Path.Combine(CatPath(subLibrary, category), "Color");
        if (string.IsNullOrEmpty(colorKey)) { if (File.Exists(file)) File.Delete(file); }
        else File.WriteAllText(file, colorKey);
    }

    public void SetCategoryFont(string subLibrary, string category, string fontKey)
    {
        var file = Path.Combine(CatPath(subLibrary, category), "Font");
        if (string.IsNullOrEmpty(fontKey)) { if (File.Exists(file)) File.Delete(file); }
        else File.WriteAllText(file, fontKey);
    }

    // ------------------------------------------------------------- 模组项

    public string CreateItem(string subLibrary, string category, string name)
    {
        var path = ItemPath(subLibrary, category, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void RenameItem(string subLibrary, string category, string oldName, string newName)
    {
        if (Directory.Exists(ItemPath(subLibrary, category, newName)))
            throw new IOException("目标文件夹已存在：" + newName);
        Directory.Move(ItemPath(subLibrary, category, oldName), ItemPath(subLibrary, category, newName));
    }

    public void MoveItem(string subLibrary, string category, string name, string targetCategory)
    {
        var target = ItemPath(subLibrary, targetCategory, name);
        if (Directory.Exists(target))
            throw new IOException("目标分类中已存在同名项：" + name);
        Directory.Move(ItemPath(subLibrary, category, name), target);
    }

    public void DeleteItem(string subLibrary, string category, string name)
    {
        var path = ItemPath(subLibrary, category, name);
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    public void SetItemFont(string itemPath, string fontKey)
    {
        var file = Path.Combine(itemPath, "Font");
        if (string.IsNullOrEmpty(fontKey)) { if (File.Exists(file)) File.Delete(file); }
        else File.WriteAllText(file, fontKey);
    }

    /// <summary>清除 config.json 备份缓存。</summary>
    public void ClearConfigCache(string itemPath)
    {
        var dir = Path.Combine(itemPath, ".config");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    /// <summary>扫描项内容（对应配置队列的内容列表：排除内部文件，标注类型与识别色）。</summary>
    public List<(string Name, string Kind, string ColorKey)> ScanItemContents(string itemPath)
    {
        var result = new List<(string, string, string)>();
        var reserved = new HashSet<string> { "README", "Version", "Code", "Code2", "README.rtf", "Font", "NexusFileName" };
        if (!Directory.Exists(itemPath)) return result;

        foreach (var dir in Directory.GetDirectories(itemPath))
        {
            var name = Path.GetFileName(dir);
            if (name is "Screenshot" or ".config" or "_MACOSX" || name.StartsWith(".")) continue;
            var color = File.Exists(Path.Combine(dir, "manifest.json")) ? "green"
                : name == "assets" ? "red"
                : name == "Content" ? "purple"
                : "white";
            result.Add((name, "文件夹", color));
        }

        foreach (var file in Directory.GetFiles(itemPath))
        {
            var name = Path.GetFileName(file);
            if (reserved.Contains(name) || name == "Color" || name == "SORT" || name == "VirtualGroup") continue;
            var color = name is "manifest.json" or "content.json" or "config.json" ? "red"
                : Path.GetExtension(name).ToLowerInvariant() is ".zip" or ".rar" or ".7z" ? "cyan"
                : "purple";
            result.Add((name, "文件", color));
        }
        return result;
    }

    // ------------------------------------------------------------- 虚拟组

    /// <summary>读取项的虚拟组列表。</summary>
    public List<string> ReadVirtualGroups(string itemPath)
    {
        var file = Path.Combine(itemPath, "VirtualGroup");
        if (!File.Exists(file)) return new List<string>();
        return File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l != "").ToList();
    }

    /// <summary>把虚拟组列表写入项（空列表则删除文件）。</summary>
    public void WriteVirtualGroups(string itemPath, IReadOnlyList<string> groups)
    {
        var file = Path.Combine(itemPath, "VirtualGroup");
        if (groups.Count == 0)
        {
            if (File.Exists(file)) File.Delete(file);
        }
        else
        {
            File.WriteAllText(file, string.Join(Environment.NewLine, groups));
        }
    }

    /// <summary>给一批项追加虚拟组（合并已有）。</summary>
    public void AddVirtualGroups(IReadOnlyList<string> itemPaths, IReadOnlyList<string> groups)
    {
        if (groups.Count == 0) return;
        foreach (var item in itemPaths)
        {
            var current = ReadVirtualGroups(item);
            var merged = new List<string>(current);
            foreach (var g in groups)
                if (!merged.Contains(g)) merged.Add(g);
            WriteVirtualGroups(item, merged);
        }
    }

    /// <summary>给一批项移除虚拟组。</summary>
    public void RemoveVirtualGroups(IReadOnlyList<string> itemPaths, IReadOnlyList<string> groups)
    {
        if (groups.Count == 0) return;
        foreach (var item in itemPaths)
        {
            var current = ReadVirtualGroups(item);
            var remaining = current.Where(g => !groups.Contains(g)).ToList();
            WriteVirtualGroups(item, remaining);
        }
    }

    /// <summary>扫描整个子库，返回 虚拟组名 → 项路径列表。</summary>
    public Dictionary<string, List<string>> CollectVirtualGroupIndex(string subLibrary)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var sub = SubPath(subLibrary);
        if (!Directory.Exists(sub)) return index;
        foreach (var cat in Directory.GetDirectories(sub))
        foreach (var item in Directory.GetDirectories(cat))
        {
            var file = Path.Combine(item, "VirtualGroup");
            if (!File.Exists(file)) continue;
            foreach (var g in File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l != ""))
            {
                if (!index.TryGetValue(g, out var list))
                    index[g] = list = new List<string>();
                list.Add(item);
            }
        }
        return index;
    }
}
