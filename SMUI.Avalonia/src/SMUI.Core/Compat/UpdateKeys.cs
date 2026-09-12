using System.Text.RegularExpressions;

namespace SmuiCore;

/// <summary>更新键解析：与 WinForms 版批量更新/检查更新的提取逻辑保持一致</summary>
public static class UpdateKeys
{
    /// <summary>从更新键文本（nexus:123|github:user/repo 形式）中提取指定平台的键值，找不到返回空字符串</summary>
    public static string Extract(string keyText, params string[] prefixes)
    {
        foreach (var key in keyText.Split('|'))
        {
            var single = key.Trim();
            var colon = single.IndexOf(':');
            if (colon < 1) continue;
            if (!prefixes.Contains(single[..colon].Trim().ToLowerInvariant())) continue;
            var value = single[(colon + 1)..].Trim();
            if (value != "") return value;
        }
        return "";
    }

    /// <summary>从 README 文本中提取更新地址：NEXUS 模组号与 GitHub 仓库，各取首个匹配链接。</summary>
    /// <returns>nexus:ID|github:用户名/仓库 格式，找不到返回空字符串</returns>
    public static string ExtractFromReadme(string readmeText)
    {
        var keys = "";
        var nexus = Regex.Match(readmeText ?? "", @"nexusmods\.com/stardewvalley/mods/(\d+)", RegexOptions.IgnoreCase);
        if (nexus.Success) keys = "nexus:" + nexus.Groups[1].Value;
        var github = Regex.Match(readmeText ?? "", @"github\.com/([A-Za-z0-9_.\-]+/[A-Za-z0-9_.\-]+)", RegexOptions.IgnoreCase);
        if (github.Success) keys += (keys == "" ? "" : "|") + "github:" + github.Groups[1].Value.TrimEnd('.');
        return keys;
    }
}
