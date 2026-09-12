using System.Text.Json;
using SMUI.Core.Util;

namespace SmuiCore.Services;

/// <summary>Mods 目录全局体检：缺 UniqueID、缺内容包依赖、缺其他依赖、依赖版本过低、
/// 需要更新 SMAPI、套娃文件夹、无意义文件夹/文件。成员名与 VB 版 ModsGlobalCheck 一致。
/// 语义修正：清单按相对路径收集，"无意义文件夹"按"目录树下是否存在 manifest"判定
/// （VB 版用全路径做前缀比较，恒误报为无意义）。</summary>
public class ModsGlobalCheck
{
    public class Data_NoContentPackType
    {
        public string UniqueId { get; set; } = "";
        public string TargetUniqueID { get; set; } = "";
    }

    public class Data_NoDependencyType
    {
        public string UniqueId { get; set; } = "";
        public string TargetUniqueID { get; set; } = "";
    }

    public class Data_DependencyVersionLowType
    {
        public string UniqueId { get; set; } = "";
        public string TargetUniqueID { get; set; } = "";
        public string MinimumVersion { get; set; } = "";
    }

    public class Data_NeedUpdateSmapiType
    {
        public string Name { get; set; } = "";
        public string UniqueId { get; set; } = "";
        public string MinimumApiVersion { get; set; } = "";
    }

    public class Data_MultiLevelFolderType
    {
        public string Name { get; set; } = "";
        public string UniqueId { get; set; } = "";
        public string Path { get; set; } = "";
    }

    public List<string> Data_Name { get; } = new();
    public List<string> Data_UniqueID { get; } = new();
    public List<string> Data_UniqueIDVerison { get; } = new();
    public List<string> Data_UniqueIDMinimumApiVersion { get; } = new();

    public List<Data_NoContentPackType> Data_NoContentPackMods { get; } = new();
    public List<Data_NoDependencyType> Data_NoDependencyMods { get; } = new();
    public List<Data_DependencyVersionLowType> Data_DependencyVersionLowMods { get; } = new();
    public List<Data_NeedUpdateSmapiType> Data_NeedUpdateSmapiMods { get; } = new();
    public List<Data_MultiLevelFolderType> Data_MultiLevelFolderMods { get; } = new();
    public List<string> Data_NoUniqueIDMods { get; } = new();
    public List<string> Data_MeaninglessFolder { get; } = new();
    public List<string> Data_MeaninglessFile { get; } = new();

    public string StartCheck(string modsFolder, string realTimeOutput = "", string smapiVersion = "", string systemPathConnector = "\\")
    {
        realTimeOutput = "";
        var connectorChar = string.IsNullOrEmpty(systemPathConnector) ? '\\' : systemPathConnector[0];
        realTimeOutput = "Scanning manifest files. . ." + Environment.NewLine;

        var search = new SearchFile();
        search.SearchManifests(modsFolder, true);
        if (search.ErrorString != "")
        {
            realTimeOutput = search.ErrorString;
            return search.ErrorString;
        }
        var manifests = search.FileCollection;

        realTimeOutput += "Reading manifest data. . ." + Environment.NewLine;
        foreach (var relative in manifests)
        {
            string text;
            try { text = File.ReadAllText(Path.Combine(modsFolder, relative)); }
            catch { continue; }
            JsonDocument doc;
            try { doc = SMUI.Core.Util.JsonHelper.ParseWithComments(text); }
            catch { continue; }
            using (doc)
            {
                var root = doc.RootElement;
                var name = GetString(root, "Name");
                if (name == "") continue;
                Data_Name.Add(name);
                var uniqueId = GetString(root, "UniqueID");
                if (uniqueId != "") Data_UniqueID.Add(uniqueId);
                else Data_NoUniqueIDMods.Add(relative);
                Data_UniqueIDVerison.Add(GetString(root, "Version"));
                Data_UniqueIDMinimumApiVersion.Add(GetString(root, "MinimumApiVersion"));
                if (relative.Split(connectorChar).Length > 3)
                {
                    Data_MultiLevelFolderMods.Add(new Data_MultiLevelFolderType
                    {
                        Name = name,
                        UniqueId = uniqueId,
                        Path = Path.GetDirectoryName(Path.Combine(modsFolder, relative)) ?? "",
                    });
                }
            }
        }

        if (smapiVersion != "")
        {
            realTimeOutput += "Comparing SMAPI versions. . ." + Environment.NewLine;
            for (var i = 0; i < Data_UniqueID.Count; i++)
            {
                if (Data_UniqueIDMinimumApiVersion[i] != ""
                    && SmuiCore.VersionUtils.CompareVersion(Data_UniqueIDMinimumApiVersion[i], smapiVersion) > 0)
                {
                    Data_NeedUpdateSmapiMods.Add(new Data_NeedUpdateSmapiType
                    {
                        Name = Data_Name[i],
                        UniqueId = Data_UniqueID[i],
                        MinimumApiVersion = Data_UniqueIDMinimumApiVersion[i],
                    });
                }
            }
        }

        realTimeOutput += "Checking dependencies. . ." + Environment.NewLine;
        foreach (var relative in manifests)
        {
            JsonDocument doc;
            try { doc = SMUI.Core.Util.JsonHelper.ParseWithComments(File.ReadAllText(Path.Combine(modsFolder, relative))); }
            catch { continue; }
            using (doc)
            {
                var root = doc.RootElement;
                if (GetString(root, "Name") == "") continue;
                if (!TryGetString(root, "UniqueID", out var uniqueId) || uniqueId == "") continue;

                string contentPackFor = "";
                if (root.TryGetProperty("ContentPackFor", out var cpf) && cpf.ValueKind == JsonValueKind.Object)
                    TryGetString(cpf, "UniqueID", out contentPackFor);
                if (contentPackFor == "") continue;
                if (Data_UniqueID.Any(u => u.Equals(contentPackFor, StringComparison.CurrentCultureIgnoreCase))) continue;
                Data_NoContentPackMods.Add(new Data_NoContentPackType { UniqueId = uniqueId, TargetUniqueID = contentPackFor });

                if (!root.TryGetProperty("Dependencies", out var deps) || deps.ValueKind != JsonValueKind.Array) continue;
                foreach (var dependency in deps.EnumerateArray())
                {
                    if (dependency.ValueKind != JsonValueKind.Object) continue;
                    if (!TryGetString(dependency, "UniqueID", out var depId) || depId == "") continue;
                    var isRequired = !TryGetString(dependency, "IsRequired", out var reqStr) || reqStr.ToLower() == "true";
                    if (!isRequired) continue;

                    if (Data_UniqueID.Any(uid => uid.Equals(depId, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        // 依赖已安装：检查版本是否过低
                        var index = Data_UniqueID.FindIndex(uid => uid.Equals(depId, StringComparison.CurrentCultureIgnoreCase));
                        if (TryGetString(dependency, "MinimumVersion", out var minimum) && minimum != ""
                            && SmuiCore.VersionUtils.CompareVersion(Data_UniqueIDVerison[index], minimum) < 0)
                        {
                            Data_DependencyVersionLowMods.Add(new Data_DependencyVersionLowType
                            {
                                UniqueId = depId,
                                TargetUniqueID = depId,
                                MinimumVersion = minimum,
                            });
                        }
                    }
                    else
                    {
                        Data_NoDependencyMods.Add(new Data_NoDependencyType { UniqueId = uniqueId, TargetUniqueID = depId });
                    }
                }
            }
        }

        realTimeOutput += "Scanning meaningless folders. . ." + Environment.NewLine;
        foreach (var folder in Directory.GetDirectories(modsFolder))
        {
            var folderName = Path.GetFileName(folder);
            var meaningful = manifests.Any(m => m.StartsWith(folderName + connectorChar, StringComparison.CurrentCultureIgnoreCase));
            if (!meaningful) Data_MeaninglessFolder.Add(folder);
        }

        realTimeOutput += "Scanning meaningless files. . ." + Environment.NewLine;
        var fileSearch = new SearchFile();
        fileSearch.SearchFiles(modsFolder, false);
        if (fileSearch.ErrorString != "")
            realTimeOutput += "An error occurred while scanning meaningless files: " + fileSearch.ErrorString + Environment.NewLine;
        else
            foreach (var file in fileSearch.FileCollection)
                Data_MeaninglessFile.Add(file);

        realTimeOutput += "Done." + Environment.NewLine;
        return "";
    }

    private static string GetString(JsonElement element, string property)
    {
        TryGetString(element, property, out var value);
        return value;
    }

    private static bool TryGetString(JsonElement element, string property, out string value)
    {
        value = "";
        foreach (var p in element.EnumerateObject())
        {
            if (p.Name.Equals(property, StringComparison.OrdinalIgnoreCase))
            {
                if (p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    value = p.Value.ToString();
                    return true;
                }
                return false;
            }
        }
        return false;
    }
}
