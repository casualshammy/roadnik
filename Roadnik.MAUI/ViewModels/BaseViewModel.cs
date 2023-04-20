using Grace.DependencyInjection;
using Roadnik.MAUI.Toolkit;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Roadnik.MAUI.ViewModels;

internal abstract class BaseViewModel : INotifyPropertyChanged
{
  public event PropertyChangedEventHandler? PropertyChanged;

  public BaseViewModel()
  {
    if (Application.Current is not ContainerizedMauiApplication app)
      throw new ApplicationException($"Application is not {nameof(ContainerizedMauiApplication)}");

    Container = app.Container;
  }

  public IInjectionScope Container { get; }

  protected bool SetProperty<T>(
    ref T _backingStore, 
    T _value,
    [CallerMemberName] string _propertyName = "",
    Action? _onChanged = null)
  {
    if (EqualityComparer<T>.Default.Equals(_backingStore, _value))
      return false;

    _backingStore = _value;
    _onChanged?.Invoke();
    OnPropertyChanged(_propertyName);
    return true;
  }

  protected void OnPropertyChanged([CallerMemberName] string _propertyName = "")
  {
    var changed = PropertyChanged;
    if (changed == null)
      return;

    changed.Invoke(this, new PropertyChangedEventArgs(_propertyName));
  }

}
