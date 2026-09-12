using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;

namespace SMUI.App.ViewModels;

public partial class InputDialogViewModel : ViewModelBase
{
    public string Title { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Multiline { get; set; }
    private string _value = "";
    public string Value { get => _value; set => SetProperty(ref _value, value); }
    public bool Confirmed { get; private set; }

    [RelayCommand]
    private void Ok()
    {
        Confirmed = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    public event Action? CloseRequested;
}

public partial class ChoiceDialogViewModel : ViewModelBase
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public System.Collections.ObjectModel.ObservableCollection<string> Options { get; } = new();

    public int SelectedIndex { get; set; } = -1;

    [RelayCommand]
    private void Select(string option)
    {
        SelectedIndex = Options.IndexOf(option);
        CloseRequested?.Invoke();
    }

    public event Action? CloseRequested;
}

public partial class MultiSelectDialogViewModel : ViewModelBase
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public System.Collections.ObjectModel.ObservableCollection<SelectableItem> Selectable { get; } = new();
    public bool Confirmed { get; private set; }

    [RelayCommand]
    private void Ok()
    {
        Confirmed = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Selectable) item.IsSelected = true;
    }

    [RelayCommand]
    private void Invert()
    {
        foreach (var item in Selectable) item.IsSelected = !item.IsSelected;
    }

    public event Action? CloseRequested;
}
