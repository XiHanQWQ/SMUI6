using System.Net.Http;

namespace SmuiCore;

/// <summary>下载状态容器：VB 版 ByRef 参数的跨语言等价物</summary>
public class DownloadState
{
    public long 已下载字节量 { get; set; }
    public long 总字节量 { get; set; }
    public bool 是否终止下载 { get; set; }
}

/// <summary>HTTP 文件下载，语义与 VB 版 下载文件 类一致：成功返回空字符串，失败/中断返回说明。
/// 区别于 VB 版：取消时返回"用户终止下载"以便调用方区分；输出目录不存在时自动创建。</summary>
public static class Downloader
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(CoreTokens.GitUserAgent);
        return client;
    }

    public static string DownloadFile(string url, string fileName, DownloadState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(fileName))!);
            using var response = Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                return $"Error: {response.StatusCode} - {response.ReasonPhrase}";
            state.总字节量 = response.Content.Headers.ContentLength ?? 0;
            using var source = response.Content.ReadAsStream();
            using var target = new FileStream(fileName, FileMode.Create);
            var buffer = new byte[102400];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (state.是否终止下载)
                    return "用户终止下载";
                target.Write(buffer, 0, read);
                state.已下载字节量 += read;
            }
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>NEXUS 专用：CDN 返回的文件名与传入不同时（x-bz-file-name 头）自动改用真实文件名。
    /// fileName 为期望的保存路径，返回时为实际保存路径。</summary>
    public static string DownloadFileFromNexus(string url, ref string fileName, DownloadState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(fileName))!);
            using var response = Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                return $"Error: {response.StatusCode} - {response.ReasonPhrase}";
            state.总字节量 = response.Content.Headers.ContentLength ?? 0;
            var cdnName = response.Headers.Contains("x-bz-file-name")
                ? response.Headers.GetValues("x-bz-file-name").FirstOrDefault()
                : null;
            if (string.IsNullOrEmpty(cdnName) && response.Content.Headers.ContentDisposition?.FileName is { } disposition)
                cdnName = disposition.Trim('"');
            if (!string.IsNullOrEmpty(cdnName))
                fileName = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(fileName))!, Path.GetFileName(cdnName));
            using var source = response.Content.ReadAsStream();
            using var target = new FileStream(fileName, FileMode.Create);
            var buffer = new byte[102400];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (state.是否终止下载)
                    return "用户终止下载";
                target.Write(buffer, 0, read);
                state.已下载字节量 += read;
            }
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
