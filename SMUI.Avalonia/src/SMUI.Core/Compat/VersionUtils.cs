using System.Text;
using System.Text.RegularExpressions;

namespace SmuiCore;

/// <summary>版本号比较，语义与 VB 版 共享方法.CompareVersion 完全一致：
/// 空/NULL 一律返回 0；先剔除非数字与点字符；按"."分段逐段数值比较，缺失段补 0。</summary>
public static class VersionUtils
{
    public static int CompareVersion(string version1, string version2)
    {
        if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2)) return 0;

        var clean1 = Regex.Replace(version1, "[^\\d\\.]", "");
        var clean2 = Regex.Replace(version2, "[^\\d\\.]", "");
        var parts1 = clean1.Split('.');
        var parts2 = clean2.Split('.');
        var max = Math.Max(parts1.Length, parts2.Length);

        for (var i = 0; i < max; i++)
        {
            var num1 = 0;
            var num2 = 0;
            if (i < parts1.Length) _ = int.TryParse(parts1[i], out num1);
            if (i < parts2.Length) _ = int.TryParse(parts2[i], out num2);

            if (num1 < num2) return -1;
            if (num1 > num2) return 1;
        }
        return 0;
    }
}

/// <summary>键值对文本/文件读写，语义与 VB 版 键值对IO操作 完全一致：
/// 整 2 段时键值均不 Trim；超过 2 段时键与拼接后的值均 Trim；
/// 写入以 CRLF 分行、UTF-8 无 BOM 覆盖写入。</summary>
public static class KeyValueIO
{
    private static void ParseInto(string text, Action<string, string> add)
    {
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Split('=');
            if (parts.Length == 2)
                add(parts[0], parts[1]);
            else if (parts.Length > 2)
                add(parts[0].Trim(), string.Join("=", parts.Skip(1)).Trim());
        }
    }

    public static string ReadFileToDictionary(Dictionary<string, string> dict, string path)
    {
        try
        {
            ParseInto(File.ReadAllText(path), (k, v) => dict[k] = v);
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string ReadTextToDictionary(Dictionary<string, string> dict, string text)
    {
        try
        {
            ParseInto(text, (k, v) => dict[k] = v);
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string ReadFileToList(List<KeyValuePair<string, string>> list, string path)
    {
        try
        {
            ParseInto(File.ReadAllText(path), (k, v) => list.Add(new KeyValuePair<string, string>(k, v)));
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string ReadTextToList(List<KeyValuePair<string, string>> list, string text)
    {
        try
        {
            ParseInto(text, (k, v) => list.Add(new KeyValuePair<string, string>(k, v)));
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string WriteDictionaryToFile(Dictionary<string, string> dict, string path)
    {
        try
        {
            var builder = new StringBuilder();
            foreach (var kv in dict)
                builder.Append(kv.Key).Append('=').Append(kv.Value).Append("\r\n");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string WriteListToFile(List<KeyValuePair<string, string>> list, string path)
    {
        try
        {
            var builder = new StringBuilder();
            foreach (var kv in list)
                builder.Append(kv.Key).Append('=').Append(kv.Value).Append("\r\n");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
