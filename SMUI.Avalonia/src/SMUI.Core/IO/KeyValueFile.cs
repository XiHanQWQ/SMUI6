using System.Text;

namespace SMUI.Core.IO;

/// <summary>
/// 键值对文本文件读写（"Key=Value" 每行一条，与 SMUI 6 的 Code2 / Settings 文件格式完全兼容）。
/// </summary>
public static class KeyValueFile
{
    public static List<KeyValuePair<string, string>> ReadPairs(string path)
    {
        var result = new List<KeyValuePair<string, string>>();
        foreach (var raw in File.ReadAllLines(path))
            ParseLine(raw, result);
        return result;
    }

    public static List<KeyValuePair<string, string>> ParseText(string text)
    {
        var result = new List<KeyValuePair<string, string>>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
            ParseLine(line, result);
        return result;
    }

    private static void ParseLine(string line, List<KeyValuePair<string, string>> target)
    {
        var parts = line.Split('=');
        if (parts.Length == 2)
        {
            target.Add(new KeyValuePair<string, string>(parts[0], parts[1]));
        }
        else if (parts.Length > 2)
        {
            var key = parts[0].Trim();
            var value = string.Join("=", parts.Skip(1)).Trim();
            target.Add(new KeyValuePair<string, string>(key, value));
        }
    }

    public static void WritePairs(string path, IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in pairs)
            sb.Append(key).Append('=').Append(value).Append("\r\n");
        File.WriteAllText(path, sb.ToString());
    }

    public static Dictionary<string, string> ReadDictionary(string path)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (key, value) in ReadPairs(path))
            dict[key] = value;
        return dict;
    }

    public static void WriteDictionary(string path, IEnumerable<KeyValuePair<string, string>> dict)
        => WritePairs(path, dict);
}
