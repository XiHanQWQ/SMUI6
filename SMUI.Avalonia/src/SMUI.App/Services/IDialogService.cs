namespace SMUI.App.Services;

/// <summary>
/// 对话框服务抽象（由 DialogService 用 Avalonia 窗口实现，供 ViewModel 调用）。
/// </summary>
public interface IDialogService
{
    /// <summary>单行文本输入对话框。取消返回 null。</summary>
    Task<string?> InputAsync(string title, string label = "", string defaultValue = "");

    /// <summary>多选一对话框。返回选择的索引，关闭窗口返回 -1。</summary>
    Task<int> ChoiceAsync(string title, string message, IReadOnlyList<string> options);

    /// <summary>确认对话框。</summary>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>信息提示对话框。</summary>
    Task InfoAsync(string title, string message);

    /// <summary>多行文本输入对话框（每行一条）。取消返回 null。</summary>
    Task<string?> MultilineInputAsync(string title, string label = "", string defaultValue = "");

    /// <summary>多选对话框（返回所有选中项）。</summary>
    Task<IReadOnlyList<string>?> MultiSelectAsync(string title, string message, IReadOnlyList<string> options);

    /// <summary>选择文件夹。</summary>
    Task<string?> PickFolderAsync(string title);

    /// <summary>选择文件（可多选）。filter 形如 ("描述", new[]{"*.zip"})。</summary>
    Task<string[]?> PickFilesAsync(string title, string filterName, params string[] patterns);

    /// <summary>保存文件。</summary>
    Task<string?> PickSaveFileAsync(string title, string defaultName, string filterName, params string[] patterns);
}
