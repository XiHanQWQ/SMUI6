using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;

namespace SMUI.Core.Services;

/// <summary>
/// 压缩包服务：zip / 7z / rar 三种格式（跨平台，替代原版的 7z.exe 外部进程方案）。
/// 导入导出包扩展名：.smuispak（子库）/.smuicpak（分类）/.smuimpak（项），本质均为 zip。
/// </summary>
public class ArchiveService
{
    /// <summary>判断文件是否为支持的压缩包。</summary>
    public static bool IsArchive(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".rar" or ".7z";

    /// <summary>解压压缩包到目标目录。</summary>
    public void Extract(string archivePath, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        using var archive = OpenArchive(archivePath);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            entry.WriteToDirectory(targetDirectory, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true,
            });
        }
    }

    /// <summary>把目录打包为 zip（导出 .smuispak/.smuicpak/.smuimpak 或通用 zip）。</summary>
    public void PackDirectory(string sourceDirectory, string targetZip)
    {
        using var archive = ZipArchive.Create();
        var rootName = Path.GetFileName(sourceDirectory.TrimEnd(Path.DirectorySeparatorChar));
        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            archive.AddEntry(Path.Combine(rootName, relative).Replace('\\', '/'), file);
        }
        archive.SaveTo(targetZip, SharpCompress.Common.CompressionType.Deflate);
    }

    /// <summary>把多个文件（不带目录层级）打包为 zip。</summary>
    public void PackFiles(IEnumerable<string> files, string targetZip)
    {
        using var archive = ZipArchive.Create();
        foreach (var file in files)
            archive.AddEntry(Path.GetFileName(file), file);
        archive.SaveTo(targetZip, SharpCompress.Common.CompressionType.Deflate);
    }

    private static IArchive OpenArchive(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".zip" or ".smuispak" or ".smuicpak" or ".smuimpak" => ZipArchive.Open(path),
            ".7z" => SevenZipArchive.Open(path),
            ".rar" => RarArchive.Open(path),
            _ => ZipArchive.Open(path),
        };
}
