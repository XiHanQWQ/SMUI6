using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SMUI.Core.Models;

namespace SMUI.App.Views;

/// <summary>依赖项表独立窗口（复刻 Form依赖项表）：列出项的内容包依赖与其他依赖。</summary>
public partial class DependenciesWindow : Window
{
    public DependenciesWindow(string itemName, string itemPath)
    {
        InitializeComponent();
        ItemTitle.Text = itemName;

        var info = new ItemInfo();
        info.Read(itemPath, new ItemInfo.ComputeFlags
        {
            ContentPackDependencies = true,
            Dependencies = true,
        });

        if (info.ErrorMessage != "")
        {
            AddNote("读取项信息失败：" + info.ErrorMessage, Brushes.OrangeRed);
            return;
        }

        AddSection("内容包依赖（ContentPackFor）");
        if (info.ContentPackDeps.Count == 0)
            AddNote("（无）", Brushes.Gray);
        else
            foreach (var kv in info.ContentPackDeps)
                AddRow($"{kv.Key}　　最低版本：{(kv.Value.MinimumVersion == "" ? "任意" : kv.Value.MinimumVersion)}");

        AddSection("其他依赖（Dependencies）");
        if (info.OtherDeps.Count == 0)
            AddNote("（无）", Brushes.Gray);
        else
            foreach (var kv in info.OtherDeps)
                AddRow($"{kv.Key}　　{(kv.Value.IsRequired ? "必须" : "可选")}　　最低版本：{(kv.Value.MinimumVersion == "" ? "任意" : kv.Value.MinimumVersion)}");
    }

    private void AddSection(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            FontSize = 13,
            Margin = new Avalonia.Thickness(0, 4, 0, 0),
        });
    }

    private void AddNote(string text, IBrush brush)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12.5,
            Foreground = brush,
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private void AddRow(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12.5,
            Margin = new Avalonia.Thickness(12, 0, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
    }
}
