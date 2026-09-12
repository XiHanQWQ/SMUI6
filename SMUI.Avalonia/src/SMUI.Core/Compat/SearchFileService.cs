using SMUI.Core.Util;

namespace SmuiCore.Services;

/// <summary>文件/清单搜索服务，成员名与 VB 版 搜索文件类 一致。
/// 与 VB 版的两处语义修正：
/// 1. SearchManifests 命中一个 manifest.json 后不再中止整棵扫描树，兄弟目录继续；
/// 2. SearchManifests 返回相对于扫描根目录的路径（便于多级检测与"无意义文件夹"判定）。</summary>
public class SearchFile
{
    /// <summary>结果集合：SearchFiles 为绝对路径；SearchManifests 为相对扫描根的路径。</summary>
    public List<string> FileCollection { get; } = new();
    public string ErrorString { get; set; } = "";

    private string _root = "";
    private bool _scanSubDirectories;
    private string _pattern = "*.*";

    public void SearchFiles(string directoryToScan, bool scanSubDirectories, string extensionName = "*.*")
    {
        ErrorString = "";
        FileCollection.Clear();
        try
        {
            _root = Path.GetFullPath(directoryToScan);
            _scanSubDirectories = scanSubDirectories;
            _pattern = extensionName;
            GetAllFiles(_root);
        }
        catch (Exception ex)
        {
            ErrorString = ex.Message;
        }
    }

    public void SearchManifests(string directoryToScan, bool scanSubDirectories = true)
    {
        ErrorString = "";
        FileCollection.Clear();
        try
        {
            _root = Path.GetFullPath(directoryToScan);
            _scanSubDirectories = scanSubDirectories;
            CollectManifestFiles(_root);
        }
        catch (Exception ex)
        {
            ErrorString = ex.Message;
        }
    }

    private void GetAllFiles(string directory)
    {
        DirectoryInfo info;
        try { info = new DirectoryInfo(directory); }
        catch (Exception ex) { ErrorString = ex.Message; return; }
        if (!info.Exists) return;
        try
        {
            foreach (var file in info.GetFiles(_pattern))
                FileCollection.Add(file.FullName);
        }
        catch (Exception ex) { ErrorString = ex.Message; }
        if (!_scanSubDirectories) return;
        try
        {
            foreach (var sub in info.GetDirectories())
                GetAllFiles(sub.FullName);
        }
        catch { }
    }

    private void CollectManifestFiles(string directory)
    {
        DirectoryInfo info;
        try { info = new DirectoryInfo(directory); }
        catch { return; }
        if (!info.Exists) return;
        try
        {
            var hit = info.GetFiles("manifest.json").FirstOrDefault();
            if (hit != null)
            {
                FileCollection.Add(Path.GetRelativePath(_root, hit.FullName).Replace('/', '\\'));
                return;
            }
        }
        catch { }
        if (!_scanSubDirectories) return;
        try
        {
            foreach (var sub in info.GetDirectories())
                CollectManifestFiles(sub.FullName);
        }
        catch { }
    }
}
