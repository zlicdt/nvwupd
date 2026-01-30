using CommunityToolkit.Mvvm.ComponentModel;

namespace NvwUpd.ViewModels;

/// <summary>
/// Base ViewModel class with common functionality.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    protected void ClearError() => ErrorMessage = null;

    protected void SetError(string message) => ErrorMessage = message;
}
