using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMUI.App.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage() => InitializeComponent();

    private void OpenNexusApiPage(object? sender, RoutedEventArgs e)
        => ViewModels.UpdatesPageViewModel.OpenUrl("https://www.nexusmods.com/users/myaccount?tab=api");
}
