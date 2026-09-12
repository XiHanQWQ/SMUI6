using System.Text.Json;
using SMUI.Core.IO;
using SMUI.Core.Util;

namespace SMUI.Core.Models;

/// <summary>内容包依赖（ContentPackFor）。</summary>
public class ContentPackDependency
{
    public string UniqueId { get; set; } = "";
    public string MinimumVersion { get; set; } = "";
}

/// <summary>其他依赖项（Dependencies 数组元素）。</summary>
public class ModDependency
{
    public string UniqueId { get; set; } = "";
    public bool IsRequired { get; set; } = true;
    public string MinimumVersion { get; set; } = "";
}

/// <summary>
/// 从模组项内所有 manifest.json 聚合读取出的项信息（对应原版 项信息读取类）。
/// </summary>
public class ItemInfo
{
    /// <summary>需要计算哪些字段（对应原版 项数据计算类型结构）。</summary>
    public class ComputeFlags
    {
        public bool All { get; set; }
        public bool InstallStatus { get; set; }
        public bool Name { get; set; }
        public bool Author { get; set; }
        public bool Version { get; set; }
        public bool InstalledVersion { get; set; }
        public bool MinimumApiVersion { get; set; }
        public bool Description { get; set; }
        public bool UniqueId { get; set; }
        public bool UpdateKeys { get; set; }
        public bool ContentPackDependencies { get; set; }
        public bool Dependencies { get; set; }

        public static ComputeFlags Full => new() { All = true };
        public static ComputeFlags StatusAndVersion => new() { InstallStatus = true, Version = true, InstalledVersion = true };
    }

    public string Status { get; set; } = InstallStatus.UnKnow;

    public List<string> Names { get; } = new();
    public List<string> Authors { get; } = new();
    public List<string> Versions { get; } = new();
    public List<string> InstalledVersions { get; } = new();
    public List<string> MinimumApiVersions { get; } = new();
    public List<string> Descriptions { get; } = new();
    public List<string> UniqueIds { get; } = new();
    public List<string> NexusIds { get; } = new();
    public List<string> ChuckleFishIds { get; } = new();
    /// <summary>GitHub 更新键的仓库部分（owner/repo）。</summary>
    public List<string> GitHubRepos { get; } = new();
    public List<string> ModDropIds { get; } = new();
    public List<string> CurseForgeIds { get; } = new();

    public Dictionary<string, ContentPackDependency> ContentPackDeps { get; } = new();
    public Dictionary<string, ModDependency> OtherDeps { get; } = new();

    public List<string> MissingFolders { get; } = new();
    public List<string> UncopiedFolders { get; } = new();
    public List<string> MissingFiles { get; } = new();

    public string ErrorMessage { get; set; } = "";

    private void Reset()
    {
        Status = InstallStatus.UnKnow;
        Names.Clear(); Authors.Clear(); Versions.Clear();
        InstalledVersions.Clear(); MinimumApiVersions.Clear(); Descriptions.Clear();
        UniqueIds.Clear(); NexusIds.Clear(); ChuckleFishIds.Clear();
        GitHubRepos.Clear(); ModDropIds.Clear(); CurseForgeIds.Clear();
        ContentPackDeps.Clear(); OtherDeps.Clear();
        MissingFolders.Clear(); UncopiedFolders.Clear(); MissingFiles.Clear();
        ErrorMessage = "";
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (!string.IsNullOrEmpty(value) && !list.Contains(value)) list.Add(value);
    }

    /// <summary>
    /// 读取项信息。项路径下必须存在 Code2 文件（旧的 Code 文件由上层先做转换）。
    /// </summary>
    public void Read(string itemPath, ComputeFlags flags, string gamePath = "")
    {
        Reset();
        ErrorMessage = "";
        if (!Directory.Exists(itemPath))
        {
            ErrorMessage = "项不存在：" + itemPath;
            return;
        }
        var code2Path = Path.Combine(itemPath, "Code2");
        if (!File.Exists(code2Path))
        {
            ErrorMessage = "项未配置：" + itemPath;
            return;
        }
        if ((flags.InstallStatus || flags.InstalledVersion || flags.All) && string.IsNullOrEmpty(gamePath))
        {
            ErrorMessage = "此计算类型需要提供游戏路径";
            return;
        }

        try
        {
            var plan = KeyValueFile.ReadPairs(code2Path);
            foreach (var (rawKey, rawValue) in plan)
            {
                var key = rawKey.Trim();
                switch (key)
                {
                    case "CD-D-MODS":
                        if (flags.InstallStatus || flags.All)
                        {
                            if (Directory.Exists(Path.Combine(gamePath, "Mods", rawValue)))
                            {
                                Status = Status switch
                                {
                                    InstallStatus.UnInstalled => InstallStatus.Incomplete,
                                    _ => InstallStatus.Installed,
                                };
                            }
                            else
                            {
                                Status = Status switch
                                {
                                    InstallStatus.Installed => InstallStatus.Incomplete,
                                    _ => InstallStatus.UnInstalled,
                                };
                                MissingFolders.Add(rawValue);
                            }
                        }
                        ReadManifests(itemPath, Path.Combine(itemPath, rawValue), flags, gamePath);
                        break;

                    case "CD-D-MODS-COVER":
                        if ((flags.InstallStatus || flags.All) && Status == InstallStatus.UnKnow)
                            Status = InstallStatus.Additional;
                        break;

                    case "CD-D-CONTENT":
                        if ((flags.InstallStatus || flags.All) && Status == InstallStatus.UnKnow)
                            Status = InstallStatus.CoverContent;
                        break;

                    case "CD-D-ROOT":
                        if (!(flags.InstallStatus || flags.All)) break;
                        {
                            var x3 = rawValue.Split('|');
                            if (x3.Length < 2) break;
                            var target = x3[1];
                            switch (Status)
                            {
                                case InstallStatus.UnKnow:
                                    if (Directory.Exists(Path.Combine(gamePath, target)))
                                        Status = InstallStatus.FolderCopied;
                                    else
                                    {
                                        Status = InstallStatus.FolderNoCopied;
                                        UncopiedFolders.Add(target);
                                    }
                                    break;
                                case InstallStatus.FolderCopied:
                                    if (!Directory.Exists(Path.Combine(gamePath, target)))
                                    {
                                        Status = InstallStatus.IncompleteFolderCopied;
                                        UncopiedFolders.Add(target);
                                    }
                                    break;
                            }
                        }
                        break;

                    case "CD-F":
                        if (!(flags.InstallStatus || flags.All)) break;
                        {
                            var x3 = rawValue.Split('|');
                            if (x3.Length < 5) break;
                            var needCheck = x3[1];
                            var needVerify = x3[2];
                            var file = x3[3];
                            var target = x3[4];
                            if (needCheck.Trim().Equals("False", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (Status == InstallStatus.UnKnow) Status = InstallStatus.File;
                                break;
                            }
                            if (!File.Exists(Path.Combine(itemPath, file)))
                            {
                                Status = InstallStatus.FileMissing;
                                break;
                            }
                            if (!File.Exists(Path.Combine(gamePath, target)))
                            {
                                switch (Status)
                                {
                                    case InstallStatus.UnKnow: Status = InstallStatus.FileUnInstalled; break;
                                    case InstallStatus.FileInstalled: Status = InstallStatus.FileIncomplete; break;
                                }
                                MissingFiles.Add(target);
                            }
                            else if (Status == InstallStatus.UnKnow)
                            {
                                Status = InstallStatus.FileInstalled;
                            }

                            if (needVerify.Trim().Equals("true", StringComparison.CurrentCultureIgnoreCase)
                                && Status is InstallStatus.FileInstalled or InstallStatus.FileInstalledVerified)
                            {
                                var a1 = VersionHelper.CalculateSha256(Path.Combine(itemPath, file));
                                var a2 = VersionHelper.CalculateSha256(Path.Combine(gamePath, target));
                                if (a1 == a2)
                                {
                                    if (Status == InstallStatus.FileInstalled) Status = InstallStatus.FileInstalledVerified;
                                }
                                else
                                {
                                    if (Status == InstallStatus.FileInstalled) Status = InstallStatus.FileInstalledVerifyfailed;
                                    MissingFiles.Add(target);
                                }
                            }
                        }
                        break;

                    case "CD-D-Advanced":
                        // 文件夹高级安装：参数 <卸载时如何操作>|<如何还原>|<要安装的文件夹>|<目标位置>
                        if (!(flags.InstallStatus || flags.All)) break;
                        {
                            var x3 = rawValue.Split('|');
                            if (x3.Length < 4) break;
                            if (Status == InstallStatus.UnKnow)
                            {
                                Status = Directory.Exists(Path.Combine(gamePath, x3[3]))
                                    ? InstallStatus.ExistedFolder
                                    : InstallStatus.FolderNotInstall;
                            }
                        }
                        break;

                    case "CR-Check-EXIST":
                    case "CR-IN-MODS-VER":
                    case "CR-UN":
                    case "CR-SHELL":
                    case "CR-MSGBOX":
                    case "CORE-CLASS":
                        break;
                }
            }

            // 依赖列表中排除自身 UniqueID
            foreach (var id in UniqueIds)
                OtherDeps.Remove(id);

            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ReadManifests(string itemPath, string folder, ComputeFlags flags, string gamePath)
    {
        var manifests = FindManifests(folder).ToList();
        var modsRoot = Path.Combine(gamePath, "Mods");
        foreach (var manifestPath in manifests)
        {
            string text;
            try { text = File.ReadAllText(manifestPath); }
            catch { continue; }

            JsonDocument doc;
            try { doc = SMUI.Core.Util.JsonHelper.ParseWithComments(text); }
            catch { continue; }
            using (doc)
            {
                var root = doc.RootElement;

                if (flags.Name || flags.All)
                {
                    if (TryGetString(root, "Name", out var name)) AddUnique(Names, name);
                    else continue;
                }
                if (flags.Author || flags.All)
                    if (TryGetString(root, "Author", out var author)) AddUnique(Authors, author);
                if (flags.Version || flags.All)
                {
                    if (TryGetString(root, "Version", out var version))
                    {
                        if (version.Contains("MajorVersion")) version = VersionHelper.FromJsonVersion(version);
                        AddUnique(Versions, version);
                    }
                }
                if (flags.InstalledVersion || flags.All)
                {
                    // 清单文件相对项根目录的路径直接映射到游戏 Mods 目录（与原版一致）
                    var rel = Path.GetRelativePath(itemPath, manifestPath);
                    var installedManifest = Path.Combine(modsRoot, rel);
                    if (File.Exists(installedManifest))
                    {
                        try
                        {
                            using var doc2 = SMUI.Core.Util.JsonHelper.ParseWithComments(File.ReadAllText(installedManifest));
                            if (TryGetString(doc2.RootElement, "Version", out var iv))
                                AddUnique(InstalledVersions, iv);
                        }
                        catch { }
                    }
                }
                if (flags.MinimumApiVersion || flags.All)
                    if (TryGetString(root, "MinimumApiVersion", out var minApi)) AddUnique(MinimumApiVersions, minApi);
                if (flags.Description || flags.All)
                    if (TryGetString(root, "Description", out var desc)) AddUnique(Descriptions, desc);
                if (flags.UniqueId || flags.All)
                    if (TryGetString(root, "UniqueID", out var uid)) AddUnique(UniqueIds, uid);

                if ((flags.UpdateKeys || flags.All) && root.TryGetProperty("UpdateKeys", out var updateKeys) && updateKeys.ValueKind == JsonValueKind.Array)
                {
                    foreach (var uk in updateKeys.EnumerateArray())
                    {
                        var ukStr = uk.ToString() ?? "";
                        var lower = ukStr.ToLowerInvariant();
                        if (lower.Contains("nexus"))
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(ukStr, ":\\s*(\\w+)");
                            if (m.Success) AddUnique(NexusIds, m.Groups[1].Value);
                        }
                        else if (lower.Contains("moddrop"))
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(ukStr, ":\\s*(\\w+)");
                            if (m.Success) AddUnique(ModDropIds, m.Groups[1].Value);
                        }
                        else if (lower.Contains("github"))
                        {
                            var repo = ExtractPlatformAddress(ukStr, "github");
                            if (repo.EndsWith("}")) repo = repo[..^1];
                            AddUnique(GitHubRepos, repo);
                        }
                        else if (lower.Contains("curseforge"))
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(ukStr, ":\\s*(\\w+)");
                            if (m.Success) AddUnique(CurseForgeIds, m.Groups[1].Value);
                        }
                        else if (lower.Contains("chucklefish"))
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(ukStr, ":\\s*(\\w+)");
                            if (m.Success) AddUnique(ChuckleFishIds, m.Groups[1].Value);
                        }
                    }
                }

                if ((flags.ContentPackDependencies || flags.All) && root.TryGetProperty("ContentPackFor", out var cpf) && cpf.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetString(cpf, "UniqueID", out var cid) && !string.IsNullOrEmpty(cid) && !ContentPackDeps.ContainsKey(cid))
                    {
                        TryGetString(cpf, "MinimumVersion", out var cmin);
                        ContentPackDeps[cid] = new ContentPackDependency { UniqueId = cid, MinimumVersion = cmin ?? "" };
                    }
                }

                if ((flags.Dependencies || flags.All) && root.TryGetProperty("Dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var dep in deps.EnumerateArray())
                    {
                        if (dep.ValueKind != JsonValueKind.Object) continue;
                        if (!TryGetString(dep, "UniqueID", out var duid) || string.IsNullOrEmpty(duid)) continue;
                        var isRequired = !TryGetString(dep, "IsRequired", out var reqStr) || reqStr.ToLower() == "true";
                        TryGetString(dep, "MinimumVersion", out var dmin);
                        if (!OtherDeps.TryGetValue(duid, out var existing))
                        {
                            OtherDeps[duid] = new ModDependency { UniqueId = duid, IsRequired = isRequired, MinimumVersion = dmin ?? "" };
                        }
                        else if (!existing.IsRequired && isRequired)
                        {
                            // 任一声明必须则按必须处理，版本取两者中较严格的
                            var cmp = VersionHelper.Compare(existing.MinimumVersion, dmin ?? "");
                            OtherDeps[duid] = new ModDependency
                            {
                                UniqueId = duid,
                                IsRequired = true,
                                MinimumVersion = cmp > 0 ? (dmin ?? "") : existing.MinimumVersion,
                            };
                        }
                    }
                }
            }
        }
    }

    /// <summary>递归查找每个子文件夹中的第一个 manifest.json（与原版 搜索清单文件 一致：每个分支只取第一个）。</summary>
    public static IEnumerable<string> FindManifests(string directory)
    {
        var manifests = new List<string>();
        CollectManifests(directory, manifests);
        return manifests;
    }

    private static void CollectManifests(string dir, List<string> manifests)
    {
        DirectoryInfo info;
        try { info = new DirectoryInfo(dir); }
        catch { return; }
        if (!info.Exists) return;
        try
        {
            var hit = info.GetFiles("manifest.json").FirstOrDefault();
            if (hit != null)
            {
                manifests.Add(hit.FullName);
                return;
            }
        }
        catch { return; }
        try
        {
            foreach (var sub in info.GetDirectories())
                CollectManifests(sub.FullName, manifests);
        }
        catch { }
    }

    /// <summary>从 "Nexus:1234" 一类的更新键中提取平台地址部分。</summary>
    public static string ExtractPlatformAddress(string oneLine, string platformName)
    {
        var a = oneLine.ToLowerInvariant();
        var b = a.IndexOf(platformName.ToLowerInvariant(), StringComparison.Ordinal);
        if (b < 0) return "";
        var c = a.IndexOf(':', b + platformName.Length);
        if (c < 0) return "";
        return oneLine[(c + 1)..].Trim();
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
