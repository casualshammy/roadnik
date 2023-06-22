using System.Windows.Input;

namespace Roadnik.MAUI.ViewModels;

internal class AboutPageViewModel : BaseViewModel
{
  private ICommand p_goWebCommand;

  public AboutPageViewModel()
  {
    p_goWebCommand = new Command(_url =>
    {
      if (_url is not string url)
        return;

      Launcher.Default.OpenAsync(url);
    });
  }

  public string Title { get; } = "About";
  public string SupportText { get; } =
    "Please leave questions, bug reports, or comments on the issue tracker. " +
    "Alternatively, you can reach me via e-mail.";
  public ICommand GoWebCommand { get => p_goWebCommand; set => SetProperty(ref p_goWebCommand, value); }
  public string AppVersion => $"Version: {AppInfo.Current.VersionString}";
  public string ThisAppUses => $"This app uses:";

}