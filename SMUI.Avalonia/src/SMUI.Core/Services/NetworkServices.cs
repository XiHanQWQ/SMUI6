using System.Net.Http;
using System.Text.Json;

namespace SMUI.Core.Services;

/// <summary>GitHub / Gitee Release 信息。</summary>
public class GitRelease
{
    public string Title { get; set; } = "";
    public string Tag { get; set; } = "";
    public bool IsPrerelease { get; set; }
    public string Body { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string Author { get; set; } = "";
    public List<KeyValuePair<string, string>> Assets { get; } = new();
}

/// <summary>
/// Git 平台 API（对应原版 GitAPI 类，GitHub + Gitee）。
/// </summary>
public class GitApiService
{
    public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36 SMUI";

    private static HttpClient? _client;
    private static HttpClient Client => _client ??= new HttpClient(new SocketsHttpHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    public enum Platform { Gitee = 1, GitHub = 2 }

    /// <summary>获取仓库最新 Release。返回错误信息，成功为空字符串。</summary>
    public async Task<string> GetLatestReleaseAsync(Platform platform, string repository, GitRelease result, string? token = null)
    {
        var url = platform switch
        {
            Platform.Gitee => $"https://gitee.com/api/v5/repos/{repository}/releases?direction=desc",
            _ => $"https://api.github.com/repos/{repository}/releases",
        };
        if (!string.IsNullOrEmpty(token))
            url += (url.Contains('?') ? "&" : "?") + "access_token=" + token;

        var (ok, content, error) = await GetAsync(url, token);
        if (!ok) return error!;

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return "Server failed to return valid data.";
            ParseRelease(doc.RootElement[0], result);
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>获取仓库全部 Release（用于模组更新选择）。</summary>
    public async Task<(List<GitRelease>, string error)> GetAllReleasesAsync(string repository, string? token = null)
    {
        var url = $"https://api.github.com/repos/{repository}/releases";
        var (ok, content, error) = await GetAsync(url, token);
        if (!ok) return (new List<GitRelease>(), error!);
        try
        {
            var list = new List<GitRelease>();
            using var doc = JsonDocument.Parse(content);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var release = new GitRelease();
                ParseRelease(el, release);
                list.Add(release);
            }
            return (list, "");
        }
        catch (Exception ex)
        {
            return (new List<GitRelease>(), ex.Message);
        }
    }

    /// <summary>获取仓库 Tag 列表。</summary>
    public async Task<(List<string>, string error)> GetTagsAsync(Platform platform, string repository, string? token = null)
    {
        var url = platform switch
        {
            Platform.Gitee => $"https://gitee.com/api/v5/repos/{repository}/tags",
            _ => $"https://api.github.com/repos/{repository}/tags",
        };
        var (ok, content, error) = await GetAsync(url, token);
        if (!ok) return (new List<string>(), error!);
        try
        {
            var tags = new List<string>();
            using var doc = JsonDocument.Parse(content);
            foreach (var el in doc.RootElement.EnumerateArray())
                if (el.TryGetProperty("name", out var n))
                    tags.Add(n.ToString() ?? "");
            return (tags, "");
        }
        catch (Exception ex)
        {
            return (new List<string>(), ex.Message);
        }
    }

    /// <summary>获取仓库内纯文本文件内容（raw）。</summary>
    public async Task<(string content, string error)> GetTextFileAsync(Platform platform, string repo, string branch, string path, string? token = null)
    {
        var url = platform switch
        {
            Platform.Gitee => $"https://gitee.com/{repo}/raw/{branch}/{path}",
            _ => $"https://raw.githubusercontent.com/{repo}/{branch}/{path}",
        };
        var (ok, content, error) = await GetAsync(url, token);
        return ok ? (content, "") : ("", error!);
    }

    private static async Task<(bool ok, string content, string? error)> GetAsync(string url, string? token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            if (!string.IsNullOrEmpty(token) && url.Contains("api.github.com"))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await Client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return (false, "", $"请求失败 ({(int)response.StatusCode}): {Truncate(content, 200)}");
            return (true, content, null);
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    private static void ParseRelease(JsonElement el, GitRelease release)
    {
        if (el.TryGetProperty("name", out var name)) release.Title = name.ToString() ?? "";
        if (el.TryGetProperty("tag_name", out var tag)) release.Tag = tag.ToString() ?? "";
        if (el.TryGetProperty("prerelease", out var pre) && pre.ValueKind is JsonValueKind.True or JsonValueKind.False)
            release.IsPrerelease = pre.GetBoolean();
        if (el.TryGetProperty("body", out var body)) release.Body = body.ToString() ?? "";
        if (el.TryGetProperty("created_at", out var created)) release.CreatedAt = created.ToString() ?? "";
        if (el.TryGetProperty("author", out var author) && author.TryGetProperty("login", out var login))
            release.Author = login.ToString() ?? "";
        if (el.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var an) && asset.TryGetProperty("browser_download_url", out var au))
                    release.Assets.Add(new KeyValuePair<string, string>(an.ToString() ?? "", au.ToString() ?? ""));
            }
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}

/// <summary>
/// HTTP 下载引擎（对应原版 下载文件 类，带进度与取消）。
/// </summary>
public class DownloadService
{
    public static async Task DownloadFileAsync(
        string url,
        string savePath,
        Action<long, long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var handler = new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.None };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(GitApiService.UserAgent);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            downloaded += read;
            progress?.Invoke(downloaded, total);
        }
    }
}
