using System.Text.Json;

namespace SmuiCore;

public enum FileType
{
    ALL = 0,
    mainFile = 1,
    optionalFile = 2,
    updateFile = 3,
    old_versionFile = 4,
    miscellaneousFile = 5,
    main_optional = 11,
    main_optional_miscellaneous = 12,
    main_optional_updateFile_miscellaneous = 13,
}

/// <summary>NEXUS 单个文件的完整信息，字段名与 VB 版 FileListDataOne 一致</summary>
public class FileListDataOne
{
    public string uid { get; set; } = "";
    public string file_id { get; set; } = "";
    public string name { get; set; } = "";
    public string version { get; set; } = "";
    public string category_id { get; set; } = "";
    public string category_name { get; set; } = "";
    public string is_primary { get; set; } = "";
    public string size { get; set; } = "";
    public string file_name { get; set; } = "";
    public string uploaded_timestamp { get; set; } = "";
    public string uploaded_time { get; set; } = "";
    public string mod_version { get; set; } = "";
    public string external_virus_scan_url { get; set; } = "";
    public string description { get; set; } = "";
    public string size_kb { get; set; } = "";
    public string changelog_html { get; set; } = "";
    public string content_preview_link { get; set; } = "";
}

internal static class NexusHttp
{
    public static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(CoreTokens.NexusUserAgent);
        return client;
    }
}

/// <summary>NEXUS 文件列表，成员名与 VB 版 GetModFileList 一致</summary>
public class GetModFileList
{
    public string ST_ApiKey { get; set; } = "";
    public List<FileListDataOne> FileListData { get; set; } = new();
    public int daily_limit { get; set; }
    public int daily_remaining { get; set; }
    public int hourly_limit { get; set; }
    public int hourly_remaining { get; set; }
    public string ErrorString { get; set; } = "";

    /// <summary>NEXUS 的 uploaded_time 为 ISO 8601 UTC 文本，转为本地时间显示（与旧版一致）；解析失败原样返回。</summary>
    private static string ParseNexusTime(string raw)
    {
        try
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return DateTimeOffset.Parse(raw, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind)
                .ToLocalTime().ToString("yyyy/M/d H:mm:ss");
        }
        catch
        {
            return raw;
        }
    }

    private static string 类别查询(FileType type) => type switch
    {
        FileType.mainFile => "category=main",
        FileType.optionalFile => "category=optional",
        FileType.updateFile => "category=update",
        FileType.old_versionFile => "category=old_version",
        FileType.miscellaneousFile => "category=miscellaneous",
        FileType.ALL => "",
        FileType.main_optional => "category=main,optional",
        FileType.main_optional_miscellaneous => "category=main,optional,miscellaneous",
        FileType.main_optional_updateFile_miscellaneous => "category=main,optional,update,miscellaneous",
        _ => "",
    };

    /// <summary>获取指定模组的文件列表。返回空字符串表示成功，否则为错误描述。</summary>
    public string StartGet(string gameName, string modId, FileType listFileType)
    {
        try
        {
            ErrorString = "";
            FileListData.Clear();
            var query = 类别查询(listFileType);
            var url = $"https://api.nexusmods.com/v1/games/{gameName}/mods/{long.Parse(modId)}/files.json" + (query == "" ? "" : "?" + query);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", ST_ApiKey);
            var response = NexusHttp.Client.SendAsync(request).GetAwaiter().GetResult();
            foreach (var header in new[] { "x-rl-daily-limit", "x-rl-daily-remaining", "x-rl-hourly-limit", "x-rl-hourly-remaining" })
            {
                if (response.Headers.TryGetValues(header, out var values))
                {
                    var value = values.FirstOrDefault() ?? "";
                    switch (header)
                    {
                        case "x-rl-daily-limit": daily_limit = int.TryParse(value, out var v1) ? v1 : 0; break;
                        case "x-rl-daily-remaining": daily_remaining = int.TryParse(value, out var v2) ? v2 : 0; break;
                        case "x-rl-hourly-limit": hourly_limit = int.TryParse(value, out var v3) ? v3 : 0; break;
                        case "x-rl-hourly-remaining": hourly_remaining = int.TryParse(value, out var v4) ? v4 : 0; break;
                    }
                }
            }
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                ErrorString = content;
                return ErrorString;
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement.Clone();
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("message", out var message))
            {
                ErrorString = message.GetString() ?? "";
                return ErrorString;
            }
            if (root.ValueKind != JsonValueKind.Array && root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in files.EnumerateArray())
                {
                    FileListData.Add(new FileListDataOne
                    {
                        uid = file.TryGetProperty("uid", out var v1) ? v1.ToString() : "",
                        file_id = file.TryGetProperty("file_id", out var v2) ? v2.ToString() : "",
                        name = file.TryGetProperty("name", out var v3) ? v3.ToString() : "",
                        version = file.TryGetProperty("version", out var v4) ? v4.ToString() : "",
                        category_id = file.TryGetProperty("category_id", out var v5) ? v5.ToString() : "",
                        category_name = file.TryGetProperty("category_name", out var v6) ? v6.ToString() : "",
                        is_primary = file.TryGetProperty("is_primary", out var v7) ? v7.ToString() : "",
                        size = file.TryGetProperty("size", out var v8) ? v8.ToString() : "",
                        file_name = file.TryGetProperty("file_name", out var v9) ? v9.ToString() : "",
                        uploaded_timestamp = file.TryGetProperty("uploaded_timestamp", out var v10) ? v10.ToString() : "",
                        uploaded_time = ParseNexusTime(file.TryGetProperty("uploaded_time", out var v11) ? v11.ToString() : ""),
                        mod_version = file.TryGetProperty("mod_version", out var v12) ? v12.ToString() : "",
                        external_virus_scan_url = file.TryGetProperty("external_virus_scan_url", out var v13) ? v13.ToString() : "",
                        description = file.TryGetProperty("description", out var v14) ? v14.ToString() : "",
                        size_kb = file.TryGetProperty("size_kb", out var v15) ? v15.ToString() : "",
                        changelog_html = file.TryGetProperty("changelog_html", out var v16) ? v16.ToString() : "",
                        content_preview_link = file.TryGetProperty("content_preview_link", out var v17) ? v17.ToString() : "",
                    });
                }
            }
            return "";
        }
        catch (Exception ex)
        {
            ErrorString = ex.Message;
            return ex.Message;
        }
    }
}

/// <summary>NEXUS 下载服务器列表，成员名与 VB 版 GetModFileDownloadURL 一致</summary>
public class GetModFileDownloadURL
{
    public string ST_ApiKey { get; set; } = "";
    public string[] name { get; set; } = Array.Empty<string>();
    public string[] short_name { get; set; } = Array.Empty<string>();
    public string[] URI { get; set; } = Array.Empty<string>();
    public int daily_limit { get; set; }
    public int daily_remaining { get; set; }
    public int hourly_limit { get; set; }
    public int hourly_remaining { get; set; }
    public string ErrorString { get; set; } = "";

    /// <summary>获取指定文件的下载服务器列表。返回空字符串表示成功，否则为错误描述。</summary>
    public string StartGet(string gameName, string modId, string fileId, string key = "", string expires = "")
    {
        try
        {
            ErrorString = "";
            var url = $"https://api.nexusmods.com/v1/games/{gameName}/mods/{long.Parse(modId)}/files/{long.Parse(fileId)}/download_link.json";
            if (key != "" && expires != "")
                url += $"?key={key}&expires={expires}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", ST_ApiKey);
            var response = NexusHttp.Client.SendAsync(request).GetAwaiter().GetResult();
            foreach (var header in new[] { "x-rl-daily-limit", "x-rl-daily-remaining", "x-rl-hourly-limit", "x-rl-hourly-remaining" })
            {
                if (response.Headers.TryGetValues(header, out var values))
                {
                    var value = values.FirstOrDefault() ?? "";
                    switch (header)
                    {
                        case "x-rl-daily-limit": daily_limit = int.TryParse(value, out var v1) ? v1 : 0; break;
                        case "x-rl-daily-remaining": daily_remaining = int.TryParse(value, out var v2) ? v2 : 0; break;
                        case "x-rl-hourly-limit": hourly_limit = int.TryParse(value, out var v3) ? v3 : 0; break;
                        case "x-rl-hourly-remaining": hourly_remaining = int.TryParse(value, out var v4) ? v4 : 0; break;
                    }
                }
            }
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                ErrorString = content;
                return ErrorString;
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement.Clone();
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("message", out var message))
            {
                ErrorString = message.GetString() ?? "";
                return ErrorString;
            }
            var servers = new List<(string name, string shortName, string uri)>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var server in root.EnumerateArray())
                {
                    servers.Add((
                        server.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        server.TryGetProperty("short_name", out var sn) ? sn.GetString() ?? "" : "",
                        server.TryGetProperty("URI", out var u) ? u.GetString() ?? "" : ""));
                }
            }
            name = servers.Select(x => x.name).ToArray();
            short_name = servers.Select(x => x.shortName).ToArray();
            URI = servers.Select(x => x.uri).ToArray();
            return "";
        }
        catch (Exception ex)
        {
            ErrorString = ex.Message;
            return ex.Message;
        }
    }
}

/// <summary>NEXUS 账号信息（密钥校验），成员名与 VB 版 GetUserInfo 一致</summary>
public class GetUserInfo
{
    public string ST_ApiKey { get; set; } = "";
    public string ErrorString { get; set; } = "";
    public string user_id { get; set; } = "";
    public string key { get; set; } = "";
    public string name { get; set; } = "";
    public string is_premium { get; set; } = "";
    public string is_supporter { get; set; } = "";
    public string email { get; set; } = "";
    public string profile_url { get; set; } = "";
    public int daily_limit { get; set; }
    public int daily_remaining { get; set; }
    public int hourly_limit { get; set; }
    public int hourly_remaining { get; set; }

    /// <summary>校验 API Key 并读取账号信息。返回空字符串表示成功，否则为错误描述。</summary>
    public string StartGet()
    {
        try
        {
            ErrorString = "";
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.nexusmods.com/v1/users/validate.json");
            request.Headers.Add("apikey", ST_ApiKey);
            var response = NexusHttp.Client.SendAsync(request).GetAwaiter().GetResult();
            foreach (var header in new[] { "x-rl-daily-limit", "x-rl-daily-remaining", "x-rl-hourly-limit", "x-rl-hourly-remaining" })
            {
                if (response.Headers.TryGetValues(header, out var values))
                {
                    var value = values.FirstOrDefault() ?? "";
                    switch (header)
                    {
                        case "x-rl-daily-limit": daily_limit = int.TryParse(value, out var v1) ? v1 : 0; break;
                        case "x-rl-daily-remaining": daily_remaining = int.TryParse(value, out var v2) ? v2 : 0; break;
                        case "x-rl-hourly-limit": hourly_limit = int.TryParse(value, out var v3) ? v3 : 0; break;
                        case "x-rl-hourly-remaining": hourly_remaining = int.TryParse(value, out var v4) ? v4 : 0; break;
                    }
                }
            }
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                ErrorString = content;
                return ErrorString;
            }
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement.Clone();
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("message", out var message))
            {
                ErrorString = message.GetString() ?? "";
                return ErrorString;
            }
            user_id = root.TryGetProperty("user_id", out var uid) ? uid.ToString() : "";
            key = root.TryGetProperty("key", out var k) ? k.ToString() : "";
            name = root.TryGetProperty("name", out var nm) ? nm.ToString() : "";
            is_premium = root.TryGetProperty("is_premium", out var ip) ? ip.ToString() : "";
            is_supporter = root.TryGetProperty("is_supporter", out var isp) ? isp.ToString() : "";
            email = root.TryGetProperty("email", out var em) ? em.ToString() : "";
            profile_url = root.TryGetProperty("profile_url", out var pu) ? pu.ToString() : "";
            return "";
        }
        catch (Exception ex)
        {
            ErrorString = ex.Message;
            return ex.Message;
        }
    }
}

public enum ListModType
{
    TheLatest10ModsReleased = 1,
    TheLatest10ModsUpdated = 2,
    The10EveryTimeHotMods = 3,
}

/// <summary>NEXUS 模组列表（最新发布/最新更新/热门），成员名与 VB 版 GetModList 一致。
/// 数组统一为 string[]：消费方仅做文本展示与拼接。</summary>
public class GetModList
{
    public string ST_ApiKey { get; set; } = "";
    public string ErrorString { get; set; } = "";
    public string[] name { get; set; } = Array.Empty<string>();
    public string[] summary { get; set; } = Array.Empty<string>();
    public string[] description { get; set; } = Array.Empty<string>();
    public string[] picture_url { get; set; } = Array.Empty<string>();
    public string[] uid { get; set; } = Array.Empty<string>();
    public string[] mod_id { get; set; } = Array.Empty<string>();
    public string[] game_id { get; set; } = Array.Empty<string>();
    public string[] allow_rating { get; set; } = Array.Empty<string>();
    public string[] domain_name { get; set; } = Array.Empty<string>();
    public string[] category_id { get; set; } = Array.Empty<string>();
    public string[] version { get; set; } = Array.Empty<string>();
    public string[] endorsement_count { get; set; } = Array.Empty<string>();
    public string[] created_timestamp { get; set; } = Array.Empty<string>();
    public string[] created_time { get; set; } = Array.Empty<string>();
    public string[] updated_timestamp { get; set; } = Array.Empty<string>();
    public string[] updated_time { get; set; } = Array.Empty<string>();
    public string[] author { get; set; } = Array.Empty<string>();
    public string[] uploaded_by { get; set; } = Array.Empty<string>();
    public string[] uploaded_users_profile_url { get; set; } = Array.Empty<string>();
    public string[] contains_adult_content { get; set; } = Array.Empty<string>();
    public string[] status { get; set; } = Array.Empty<string>();
    public string[] available { get; set; } = Array.Empty<string>();
    public int daily_limit { get; set; }
    public int daily_remaining { get; set; }
    public int hourly_limit { get; set; }
    public int hourly_remaining { get; set; }

    /// <summary>获取在线模组列表。返回空字符串表示成功，否则为错误描述。</summary>
    public string StartGet(string gameName, ListModType listType)
    {
        try
        {
            ErrorString = "";
            var endpoint = listType switch
            {
                ListModType.TheLatest10ModsReleased => "latest_added.json",
                ListModType.TheLatest10ModsUpdated => "latest_updated.json",
                ListModType.The10EveryTimeHotMods => "trending.json",
                _ => throw new ArgumentOutOfRangeException(nameof(listType)),
            };
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.nexusmods.com/v1/games/{gameName}/mods/{endpoint}");
            request.Headers.Add("apikey", ST_ApiKey);
            var response = NexusHttp.Client.SendAsync(request).GetAwaiter().GetResult();
            foreach (var header in new[] { "x-rl-daily-limit", "x-rl-daily-remaining", "x-rl-hourly-limit", "x-rl-hourly-remaining" })
            {
                if (response.Headers.TryGetValues(header, out var values))
                {
                    var value = values.FirstOrDefault() ?? "";
                    switch (header)
                    {
                        case "x-rl-daily-limit": daily_limit = int.TryParse(value, out var v1) ? v1 : 0; break;
                        case "x-rl-daily-remaining": daily_remaining = int.TryParse(value, out var v2) ? v2 : 0; break;
                        case "x-rl-hourly-limit": hourly_limit = int.TryParse(value, out var v3) ? v3 : 0; break;
                        case "x-rl-hourly-remaining": hourly_remaining = int.TryParse(value, out var v4) ? v4 : 0; break;
                    }
                }
            }
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                ErrorString = content;
                return ErrorString;
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement.Clone();
            var rows = new List<Dictionary<string, string>>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var mod in root.EnumerateArray())
                {
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in mod.EnumerateObject())
                        row[prop.Name] = prop.Value.ToString();
                    rows.Add(row);
                }
            }

            string? Get(Dictionary<string, string> row, string key) =>
                row.TryGetValue(key, out var value) ? value : null;

            name = rows.Select(r => Get(r, "name") ?? "").ToArray();
            summary = rows.Select(r => Get(r, "summary") ?? "").ToArray();
            description = rows.Select(r => Get(r, "description") ?? "").ToArray();
            picture_url = rows.Select(r => Get(r, "picture_url") ?? "").ToArray();
            uid = rows.Select(r => Get(r, "uid") ?? "").ToArray();
            mod_id = rows.Select(r => Get(r, "mod_id") ?? "").ToArray();
            game_id = rows.Select(r => Get(r, "game_id") ?? "").ToArray();
            allow_rating = rows.Select(r => Get(r, "allow_rating") ?? "").ToArray();
            domain_name = rows.Select(r => Get(r, "domain_name") ?? "").ToArray();
            category_id = rows.Select(r => Get(r, "category_id") ?? "").ToArray();
            version = rows.Select(r => Get(r, "version") ?? "").ToArray();
            endorsement_count = rows.Select(r => Get(r, "endorsement_count") ?? "").ToArray();
            created_timestamp = rows.Select(r => Get(r, "created_timestamp") ?? "").ToArray();
            created_time = rows.Select(r => Get(r, "created_time") ?? "").ToArray();
            updated_timestamp = rows.Select(r => Get(r, "updated_timestamp") ?? "").ToArray();
            updated_time = rows.Select(r => Get(r, "updated_time") ?? "").ToArray();
            author = rows.Select(r => Get(r, "author") ?? "").ToArray();
            uploaded_by = rows.Select(r => Get(r, "uploaded_by") ?? "").ToArray();
            uploaded_users_profile_url = rows.Select(r => Get(r, "uploaded_users_profile_url") ?? "").ToArray();
            contains_adult_content = rows.Select(r => Get(r, "contains_adult_content") ?? "").ToArray();
            status = rows.Select(r => Get(r, "status") ?? "").ToArray();
            available = rows.Select(r => Get(r, "available") ?? "").ToArray();
            return "";
        }
        catch (Exception ex)
        {
            ErrorString = ex.Message;
            return ex.Message;
        }
    }
}

