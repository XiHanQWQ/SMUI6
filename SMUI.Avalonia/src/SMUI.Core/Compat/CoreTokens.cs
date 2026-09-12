namespace SmuiCore;

/// <summary>
/// 核心库运行参数：由宿主（WinForms 壳）在启动与设置变更时写入，
/// 核心库自身不读取任何配置文件，保持无 UI、无本地状态依赖。
/// </summary>
public static class CoreTokens
{
    /// <summary>GitHub Personal Access Token（api.github.com / raw.githubusercontent.com 匿名限额提升）</summary>
    public static string GitHubToken = "";

    /// <summary>Gitee Personal Access Token（gitee.com/api 限额提升、支持私有仓库）</summary>
    public static string GiteeToken = "";

    /// <summary>NEXUS 个人 API Key（文件列表与下载地址接口必需）</summary>
    public static string NexusApiKey = "";

    /// <summary>NEXUS API 的 User-Agent</summary>
    public static string NexusUserAgent = "NEXUS API (Windows NT; WOW64) from SMUI 6";

    /// <summary>Git 平台请求的 User-Agent</summary>
    public static string GitUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36 Edg/117.0.2045.47";
}
