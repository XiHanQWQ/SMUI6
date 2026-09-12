using System.Text.RegularExpressions;

namespace SMUI.Core.Util;

public static partial class VersionHelper
{
    /// <summary>
    /// 语义化版本比较（与原版 CompareVersion 规则一致：提取数字与小数点逐段比较，缺失段视为 0）。
    /// 返回 -1 / 0 / 1。
    /// </summary>
    public static int Compare(string? version1, string? version2)
    {
        if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2)) return 0;

        var clean1 = NonNumericRegex().Replace(version1, "");
        var clean2 = NonNumericRegex().Replace(version2, "");
        var arr1 = clean1.Split('.');
        var arr2 = clean2.Split('.');
        var max = Math.Max(arr1.Length, arr2.Length);

        for (var i = 0; i < max; i++)
        {
            var n1 = i < arr1.Length && int.TryParse(arr1[i], out var t1) ? t1 : 0;
            var n2 = i < arr2.Length && int.TryParse(arr2[i], out var t2) ? t2 : 0;
            if (n1 < n2) return -1;
            if (n1 > n2) return 1;
        }
        return 0;
    }

    [GeneratedRegex("[^\\d\\.]")]
    private static partial Regex NonNumericRegex();

    public static string CalculateSha256(string filePath)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(filePath);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>从 manifest 的 Version 字段提取 MajorVersion/MinorVersion/... JSON 对象形式的语义版本号。</summary>
    public static string FromJsonVersion(string jsonText)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
            var root = doc.RootElement;
            if (!root.TryGetProperty("MajorVersion", out var major)) return "";
            var sb = new System.Text.StringBuilder(major.ToString());
            if (root.TryGetProperty("MinorVersion", out var minor) && minor.ValueKind != System.Text.Json.JsonValueKind.Null)
                sb.Append('.').Append(minor);
            if (root.TryGetProperty("PatchVersion", out var patch) && patch.ValueKind != System.Text.Json.JsonValueKind.Null)
                sb.Append('.').Append(patch);
            if (root.TryGetProperty("Build", out var build) && build.ValueKind != System.Text.Json.JsonValueKind.Null)
                sb.Append('.').Append(build);
            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }
}
