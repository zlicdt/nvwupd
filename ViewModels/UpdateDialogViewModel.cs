using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvwUpd.Models;

namespace NvwUpd.ViewModels;

/// <summary>
/// ViewModel for the update dialog.
/// </summary>
public partial class UpdateDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _currentVersion = "--";

    [ObservableProperty]
    private string _newVersion = "--";

    [ObservableProperty]
    private string _releaseDate = "--";

    [ObservableProperty]
    private string? _releaseNotes;

    [ObservableProperty]
    private DriverType _selectedDriverType = DriverType.GameReady;

    [ObservableProperty]
    private bool _isGameReadySelected = true;

    [ObservableProperty]
    private bool _isStudioSelected;

    public Action<bool>? CloseAction { get; set; }

    partial void OnIsGameReadySelectedChanged(bool value)
    {
        if (value)
        {
            SelectedDriverType = DriverType.GameReady;
            IsStudioSelected = false;
        }
    }

    partial void OnIsStudioSelectedChanged(bool value)
    {
        if (value)
        {
            SelectedDriverType = DriverType.Studio;
            IsGameReadySelected = false;
        }
    }

    public void SetDriverInfo(GpuInfo gpuInfo, DriverInfo driverInfo)
    {
        CurrentVersion = gpuInfo.DriverVersion;
        NewVersion = driverInfo.Version;
        ReleaseDate = driverInfo.ReleaseDate.ToString("yyyy-MM-dd");
        ReleaseNotes = driverInfo.ReleaseNotes;
    }

    [RelayCommand]
    private void Confirm()
    {
        CloseAction?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseAction?.Invoke(false);
    }
}
