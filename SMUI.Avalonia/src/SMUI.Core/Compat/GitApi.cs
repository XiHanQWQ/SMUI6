using System.Text.Json;

namespace SmuiCore.GitApi;

public enum 开源代码平台
{
    Gitee = 1,
    GitHub = 2,
}

/// <summary>发行版附件（文件名 + 下载直链）</summary>
public class 发行版附件
{
    public string 文件名 { get; set; } = "";
    public string 下载地址 { get; set; } = "";
}

/// <summary>GitHub 单个发行版（含全部附件），对应 VB 版 GitHubAllReleaseFile 的数据形状</summary>
public class GitHub发行版
{
    public string 标题 { get; set; } = "";
    public string 描述 { get; set; } = "";
    public string 标签 { get; set; } = "";
    public bool 是否是草稿 { get; set; }
    public bool 是否预览版 { get; set; }
    public List<KeyValuePair<string, string>> 可供下载的文件 { get; set; } = new();
}

internal static class GitHttp
{
    public static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(CoreTokens.GitUserAgent);
        return client;
    }

    /// <summary>按域名附加平台令牌：GitHub 走 Bearer 头，Gitee API 走 access_token 查询参数。返回最终请求地址。</summary>
    public static string 附加平台令牌(string 请求地址)
    {
        if (请求地址.StartsWith("https://api.github.com") || 请求地址.StartsWith("https://raw.githubusercontent.com"))
        {
            var token = CoreTokens.GitHubToken?.Trim();
            if (!string.IsNullOrEmpty(token))
                Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        else if (请求地址.StartsWith("https://gitee.com/api/"))
        {
            var token = CoreTokens.GiteeToken?.Trim();
            if (!string.IsNullOrEmpty(token))
                请求地址 += (请求地址.Contains('?') ? "&" : "?") + "access_token=" + token;
            Client.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            Client.DefaultRequestHeaders.Authorization = null;
        }
        return 请求地址;
    }

    public static string GetString(string url)
    {
        var final = 附加平台令牌(url);
        return Client.GetStringAsync(final).GetAwaiter().GetResult();
    }

    /// <summary>解析 GitHub/Gitee 的列表响应：数组正常返回；对象则视为错误并提取 message。</summary>
    public static JsonElement 解析列表响应(string content, out string error)
    {
        error = "";
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement.Clone();
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("message", out var message))
        {
            error = message.GetString() ?? "";
        }
        return root;
    }
}

/// <summary>最新单个发行版（SMUI 自身更新检查用），成员名与 VB 版 Release 一致</summary>
public class Release
{
    public string 发布标题 { get; set; } = "";
    public string 版本标签 { get; set; } = "";
    public bool 预览版 { get; set; }
    public string 发布描述 { get; set; } = "";
    public string 发布时间 { get; set; } = "";
    public string 发布者用户名 { get; set; } = "";
    public string 发布者昵称 { get; set; } = "";
    public List<KeyValuePair<string, string>> 可供下载的文件 { get; set; } = new();
    public string ErrorString { get; set; } = "";

    public string 获取仓库发布版信息(开源代码平台 目标平台, string 存储库)
    {
        var url = 目标平台 == 开源代码平台.Gitee
            ? $"https://gitee.com/api/v5/repos/{存储库}/releases/?direction=desc"
            : $"https://api.github.com/repos/{存储库}/releases";
        try
        {
            ErrorString = "";
            var content = GitHttp.GetString(url);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 1)
            {
                ErrorString = "Server failed to return valid data.";
                return ErrorString;
            }
            var first = root[0];
            发布标题 = first.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            版本标签 = first.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            预览版 = first.TryGetProperty("prerelease", out var p) && p.ToString() == "True";
            发布描述 = first.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            发布时间 = first.TryGetProperty("created_at", out var c) ? c.GetString() ?? "" : "";
            if (first.TryGetProperty("author", out var author))
            {
                发布者用户名 = author.TryGetProperty("login", out var l) ? l.GetString() ?? "" : "";
                if (目标平台 == 开源代码平台.Gitee)
                    发布者昵称 = author.TryGetProperty("name", out var nn) ? nn.GetString() ?? "" : "";
            }
            if (first.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var an) ? an.GetString() : null;
                    var download = asset.TryGetProperty("browser_download_url", out var bu) ? bu.GetString() : null;
                    if (name != null && download != null)
                        可供下载的文件.Add(new KeyValuePair<string, string>(name, download));
                }
            }
            return "";
        }
        catch (Exception ex)
        {
            ErrorString = ex.Message;
            return ErrorString;
        }
    }
}

/// <summary>仓库全部发行版（模组 GitHub 更新用），成员名与 VB 版 GitHubAllReleaseFile 一致</summary>
public class GitHubAllReleaseFile
{
    public class 发行版单片
    {
        public string 标题 { get; set; } = "";
        public string 描述 { get; set; } = "";
        public string 标签 { get; set; } = "";
        public bool 是否是草稿 { get; set; }
        public bool 是否预览版 { get; set; }
        public List<KeyValuePair<string, string>> 可供下载的文件 { get; set; } = new();
    }

    public List<发行版单片> 发行版数据集合 { get; set; } = new();
    public string ErrorString { get; set; } = "";

    public string 获取(string 存储库)
    {
        try
        {
            ErrorString = "";
            发行版数据集合.Clear();
            var content = GitHttp.GetString($"https://api.github.com/repos/{存储库}/releases");
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Array)
            {
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("message", out var message))
                    ErrorString = message.GetString() ?? "";
                else
                    ErrorString = "Server failed to return valid data.";
                return ErrorString;
            }
            foreach (var rel in root.EnumerateArray())
            {
                var item = new 发行版单片
                {
                    标题 = rel.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    描述 = rel.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                    标签 = rel.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "",
                    是否是草稿 = rel.TryGetProperty("draft", out var d) && d.ToString() == "True",
                    是否预览版 = rel.TryGetProperty("prerelease", out var p) && p.ToString() == "True",
                };
                if (rel.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var an) ? an.GetString() : null;
                        var download = asset.TryGetProperty("browser_download_url", out var bu) ? bu.GetString() : null;
                        if (name != null && download != null)
                            item.可供下载的文件.Add(new KeyValuePair<string, string>(name, download));
                    }
                }
                发行版数据集合.Add(item);
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

/// <summary>仓库 Tag 列表，成员名与 VB 版 Tag 一致</summary>
public class Tag
{
    public class Tag单片数据
    {
        public string name { get; set; } = "";
    }

    public List<Tag单片数据> Tag数据 { get; set; } = new();
    public string ErrorString { get; set; } = "";

    public string 获取仓库Tag信息(开源代码平台 目标平台, string 存储库)
    {
        var url = 目标平台 == 开源代码平台.Gitee
            ? $"https://gitee.com/api/v5/repos/{存储库}/tags"
            : $"https://api.github.com/repos/{存储库}/tags";
        try
        {
            Tag数据.Clear();
            ErrorString = "";
            var content = GitHttp.GetString(url);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Array)
            {
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("message", out var message))
                    ErrorString = message.GetString() ?? "";
                else
                    ErrorString = "Server failed to return valid data.";
                return ErrorString;
            }
            foreach (var tag in root.EnumerateArray())
            {
                if (tag.TryGetProperty("name", out var name))
                    Tag数据.Add(new Tag单片数据 { name = name.GetString() ?? "" });
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

/// <summary>raw 纯文本文件获取（新闻 INI 等），成员名与 VB 版 TextFileString 一致</summary>
public class TextFileString
{
    public string 网页返回字符串 { get; set; } = "";
    public string ErrorString { get; set; } = "";

    public string 获取文本文件数据(开源代码平台 目标平台, string 用户名和仓库名, string 分支, string 路径,
        string 令牌 = "", bool 是否需要进行Json错误消息识别 = false)
    {
        try
        {
            ErrorString = "";
            var url = 目标平台 == 开源代码平台.Gitee
                ? $"https://gitee.com/{用户名和仓库名}/raw/{分支}/{路径}"
                : $"https://raw.githubusercontent.com/{用户名和仓库名}/{分支}/{路径}";
            if (!string.IsNullOrEmpty(令牌))
            {
                url += 目标平台 == 开源代码平台.Gitee
                    ? "?access_token=" + 令牌
                    : "";
            }
            var request = GitHttp.附加平台令牌(url);
            if (!string.IsNullOrEmpty(令牌) && 目标平台 == 开源代码平台.GitHub)
                GitHttp.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", 令牌);
            var content = GitHttp.Client.GetStringAsync(request).GetAwaiter().GetResult();

            if (是否需要进行Json错误消息识别)
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("message", out var message))
                {
                    ErrorString = message.GetString() ?? "";
                    return ErrorString;
                }
                网页返回字符串 = content;
            }
            else
            {
                网页返回字符串 = content;
            }
        }
        catch (Exception ex)
        {
            ErrorString = ex.Message;
        }
        return ErrorString;
    }
}
