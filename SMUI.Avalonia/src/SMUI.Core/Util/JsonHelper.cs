using System.Text.Json;
using System.Text.RegularExpressions;

namespace SMUI.Core.Util;

/// <summary>JSON 工具：剥离 manifest.json 中的注释（部分模组作者会在 manifest.json 里写 /* */ 或 // 注释，
/// 标准 JSON 不支持注释，System.Text.Json 解析会失败）。</summary>
public static partial class JsonHelper
{
    /// <summary>剥离 JSON 文本中的 /* */ 块注释和 // 行注释（保护字符串值内的 //，如 https:// 链接）。</summary>
    public static string StripComments(string json)
    {
        if (string.IsNullOrEmpty(json)) return json ?? "";
        // 1) 去块注释 /* ... */（非贪婪，跨行）
        var s = Regex.Replace(json, @"/\*[\s\S]*?\*/", "");
        // 2) 去行注释 // ... 到行尾（但不匹配字符串内的 ://，如 https://）
        s = Regex.Replace(s, @"(?<!:)//[^\r\n""]*", "");
        return s;
    }

    /// <summary>解析可能含注释的 JSON 文本为 JsonDocument。</summary>
    public static JsonDocument ParseWithComments(string json)
    {
        var clean = StripComments(json);
        return JsonDocument.Parse(clean, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
    }
}
