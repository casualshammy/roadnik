using CommunityToolkit.Maui.Views;
using System.ComponentModel;
using System.Windows.Input;

namespace Roadnik.MAUI.Controls;

public partial class AgreementsPopup : Popup, INotifyPropertyChanged
{
  public AgreementsPopup(Action<bool> _onAgreedCallback)
	{
    InitializeComponent();
    BindingContext = this;

    GoWebCommand = new Command(_url =>
    {
      if (_url is not string url)
        return;

      Launcher.Default.OpenAsync(url);
    });
    OnPropertyChanged(nameof(GoWebCommand));

    CloseCommand = new Command(_agreed =>
    {
      _onAgreedCallback(true);
      CloseAsync();
    });
    OnPropertyChanged(nameof(CloseCommand));
  }

  public ICommand GoWebCommand { get; }
  public ICommand CloseCommand { get; }

}