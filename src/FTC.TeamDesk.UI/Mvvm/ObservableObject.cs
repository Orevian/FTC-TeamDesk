using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FTC.TeamDesk.UI.Mvvm;

/// <summary>Minimal INotifyPropertyChanged base. No UI-framework types, so view models stay platform independent and testable.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? name = null)
    {
        if (!SetProperty(ref field, value, name)) return false;
        onChanged();
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected void Raise(params string[] names) { foreach (var n in names) OnPropertyChanged(n); }
}
