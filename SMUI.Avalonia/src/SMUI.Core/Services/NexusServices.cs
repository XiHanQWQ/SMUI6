using System.Net.Http;
using System.Text.Json;

namespace SMUI.Core.Services;

/// <summary>NEXUS 用户信息。</summary>
public class NexusUserInfo
{
    public string Name { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsPremium { get; set; }
    public bool IsSupporter { get; set; }
    public int HourlyRemaining { get; set; }
    public int HourlyLimit { get; set; }
    public int DailyRemaining { get; set; }
    public int DailyLimit { get; set; }
}

/// <summary>NEXUS 模组文件条目。</summary>
public class NexusModFile
{
    public long FileId { get; set; }
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public long Size { get; set; }
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime UploadedTime { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// NEXUS MODS API 客户端（对应原版 NEXUS 类）。
/// </summary>
public class NexusApiService
{
    private const string Base = "https://api.nexusmods.com/v1";
    public string ApiKey { get; set; } = "";
    public string GameName { get; set; } = "stardewvalley";

    private static HttpClient? _client;
    private static HttpClient Client => _client ??= new HttpClient(new SocketsHttpHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private async Task<(bool ok, string content, string? error)> GetAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(GitApiService.UserAgent);
            if (!string.IsNullOrEmpty(ApiKey))
                request.Headers.Add("apikey", ApiKey);
            using var response = await Client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return (false, "", $"NEXUS API 错误 ({(int)response.StatusCode}): {Truncate(content, 200)}");
            return (true, content, null);
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    /// <summary>验证 API Key 并获取用户信息。</summary>
    public async Task<(NexusUserInfo? info, string error)> ValidateAsync()
    {
        var (ok, content, error) = await GetAsync($"{Base}/users/validate.json");
        if (!ok) return (null, error!);
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var info = new NexusUserInfo
            {
                UserId = GetString(root, "user_id"),
                Name = GetString(root, "name"),
                Email = GetString(root, "email"),
                IsPremium = GetString(root, "is_premium") == "True",
                IsSupporter = GetString(root, "is_supporter") == "True",
            };
            return (info, "");
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>获取模组文件列表（category: main/optional/update/old_version/miscellaneous 或组合）。</summary>
    public async Task<(List<NexusModFile>, string error)> GetModFilesAsync(int modId, string category = "main")
    {
        var url = $"{Base}/games/{GameName}/mods/{modId}/files.json";
        if (category != "all") url += $"?category={category}";
        var (ok, content, error) = await GetAsync(url);
        if (!ok) return (new List<NexusModFile>(), error!);
        try
        {
            var files = new List<NexusModFile>();
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("files", out var arr)) return (files, "");
            foreach (var el in arr.EnumerateArray())
            {
                var file = new NexusModFile
                {
                    FileId = el.TryGetProperty("file_id", out var fid) && fid.TryGetInt64(out var f) ? f : 0,
                    Name = GetString(el, "file_name"),
                    Version = GetString(el, "version"),
                    Size = el.TryGetProperty("size", out var size) && size.TryGetInt64(out var s) ? s : 0,
                    Category = GetString(el, "category_name"),
                    Description = GetString(el, "description"),
                    IsPrimary = GetString(el, "is_primary") == "True",
                };
                if (el.TryGetProperty("uploaded_time", out var ut) && DateTime.TryParse(ut.ToString(), out var dt))
                    file.UploadedTime = dt;
                files.Add(file);
            }
            return (files, "");
        }
        catch (Exception ex)
        {
            return (new List<NexusModFile>(), ex.Message);
        }
    }

    /// <summary>获取模组文件的下载地址（返回多个 CDN 镜像）。</summary>
    public async Task<(List<string>, string error)> GetDownloadUrlsAsync(int modId, long fileId, string? key = null, string? expires = null)
    {
        var url = $"{Base}/games/{GameName}/mods/{modId}/files/{fileId}/download_link.json";
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(expires))
            url += $"?key={key}&expires={expires}";
        var (ok, content, error) = await GetAsync(url);
        if (!ok) return (new List<string>(), error!);
        try
        {
            var urls = new List<string>();
            using var doc = JsonDocument.Parse(content);
            foreach (var el in doc.RootElement.EnumerateArray())
                if (el.TryGetProperty("URI", out var uri))
                    urls.Add(uri.ToString() ?? "");
            return (urls, "");
        }
        catch (Exception ex)
        {
            return (new List<string>(), ex.Message);
        }
    }

    private static string GetString(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object) return "";
        foreach (var p in el.EnumerateObject())
            if (p.Name.Equals(prop, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind != JsonValueKind.Null)
                return p.Value.ToString();
        return "";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}

/// <summary>smapi.io 检查更新的结果条目。</summary>
public class SmapiUpdateResult
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SuggestedVersion { get; set; } = "";
    public string SuggestedUrl { get; set; } = "";
    public string MainVersion { get; set; } = "";
    public string MainUrl { get; set; } = "";
    public string NexusId { get; set; } = "";
    public string ModDropId { get; set; } = "";
    public string GitHubRepo { get; set; } = "";
    public string CurseForgeId { get; set; } = "";
    public string CompatibilitySummary { get; set; } = "";
}

/// <summary>
/// SMAPI 云服务客户端（对应原版 SMAPI云服务 类，smapi.io 批量模组更新检查）。
/// </summary>
public class SmapiCloudService
{
    public record ModQuery(string Id, List<string> UpdateKeys, string InstalledVersion);

    public async Task<(List<SmapiUpdateResult>, string error)> CheckModsAsync(IEnumerable<ModQuery> mods)
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["apiVersion"] = "3.0.0",
                ["gameVersion"] = "",
                ["platform"] = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "Mac" : "Linux",
                ["includeExtendedMetadata"] = true,
                ["mods"] = mods.Select(m => new Dictionary<string, object?>
                {
                    ["id"] = m.Id,
                    ["updatekeys"] = m.UpdateKeys,
                    ["installedversion"] = m.InstalledVersion,
                }).ToList(),
            };

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WEB API");
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("https://smapi.io/api/v3.0/mods", content);
            if (!response.IsSuccessStatusCode)
                return (new List<SmapiUpdateResult>(), $"Error: {response.StatusCode}");

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var results = new List<SmapiUpdateResult>();
            if (!doc.RootElement.TryGetProperty("Data", out var data)) return (results, "");
            foreach (var el in data.EnumerateArray())
            {
                var r = new SmapiUpdateResult
                {
                    Id = Str(el, "id"),
                };
                if (el.TryGetProperty("suggestedUpdate", out var su) && su.ValueKind == JsonValueKind.Object)
                {
                    r.SuggestedVersion = Str(su, "version");
                    r.SuggestedUrl = Str(su, "url");
                }
                if (el.TryGetProperty("metadata", out var md) && md.ValueKind == JsonValueKind.Object)
                {
                    r.Name = Str(md, "name");
                    r.NexusId = Str(md, "nexusID");
                    r.ModDropId = Str(md, "modDropID");
                    r.GitHubRepo = Str(md, "gitHubRepo");
                    r.CurseForgeId = Str(md, "curseForgeID");
                    r.CompatibilitySummary = Str(md, "compatibilitySummary");
                    if (md.TryGetProperty("main", out var main) && main.ValueKind == JsonValueKind.Object)
                    {
                        r.MainVersion = Str(main, "version");
                        r.MainUrl = Str(main, "url");
                    }
                }
                results.Add(r);
            }
            return (results, "");
        }
        catch (Exception ex)
        {
            return (new List<SmapiUpdateResult>(), ex.Message);
        }
    }

    private static string Str(JsonElement el, string prop)
    {
        foreach (var p in el.EnumerateObject())
            if (p.Name.Equals(prop, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind != JsonValueKind.Null)
                return p.Value.ToString();
        return "";
    }
}
