namespace Roadnik.MAUI.ViewModels;

internal class MainPageViewModel : BaseViewModel
{
  private bool p_isSpinnerRequired;
  private string? p_webViewUrl;
  private bool p_permissionViewRequired;
  private bool p_remoteServerIsNotResponding;
  private bool p_isInBackground;
  private string p_title;

  public MainPageViewModel()
  {
    p_isSpinnerRequired = true;
    p_webViewUrl = null;
    p_permissionViewRequired = false;
    p_remoteServerIsNotResponding = false;
    p_isInBackground = false;
    p_title = "Roadnik";
  }

  public string Title { get => p_title; set => SetProperty(ref p_title, value); }
  public bool IsSpinnerRequired { get => p_isSpinnerRequired; set => SetProperty(ref p_isSpinnerRequired, value); }
  public string? WebViewUrl { get => p_webViewUrl; set => SetProperty(ref p_webViewUrl, value); }

  public bool IsPermissionWindowShowing
  {
    get => p_permissionViewRequired;
    set
    {
      SetProperty(ref p_permissionViewRequired, value);
      OnPropertyChanged(nameof(CanShowRegilarView));
      OnPropertyChanged(nameof(CanShowPermissionView));
      OnPropertyChanged(nameof(CanShowConnectionErrorView));
    }
  }
  public bool IsRemoteServerNotResponding
  {
    get => p_remoteServerIsNotResponding;
    set
    {
      SetProperty(ref p_remoteServerIsNotResponding, value);
      OnPropertyChanged(nameof(CanShowRegilarView));
      OnPropertyChanged(nameof(CanShowConnectionErrorView));
    }
  }
  public bool IsInBackground
  {
    get => p_isInBackground;
    set
    {
      SetProperty(ref p_isInBackground, value);
      OnPropertyChanged(nameof(CanShowRegilarView));
      OnPropertyChanged(nameof(CanShowPermissionView));
      OnPropertyChanged(nameof(CanShowConnectionErrorView));
    }
  }

  public bool CanShowRegilarView => !p_permissionViewRequired && !p_remoteServerIsNotResponding && !p_isInBackground;
  public bool CanShowPermissionView => p_permissionViewRequired && !p_isInBackground;
  public bool CanShowConnectionErrorView => p_remoteServerIsNotResponding && !p_permissionViewRequired && !p_isInBackground;

  public string LocationPermissionDescription { get; } =
    "Our app requires access to your device's background location in order to provide you with accurate and up-to-date location tracking. " +
    "This means that even when our app is not shown on the screen, we're still able to keep track of your location in the background.\r\n\r\n" +
    "Additionally, if you choose to share your location information with others through our remote server feature, " +
    "we need access to your background location so that we can continuously update your location and share it with other users in real-time.\r\n\r\n" +
    "We understand that privacy is important to you, and we want to assure you that we take the protection of your personal data seriously. " +
    "Rest assured that we will only collect and use your location information in accordance with our privacy policy, " +
    "which is designed to protect your data and ensure that it is only used for the purposes you have explicitly consented to.\r\n\r\n" +
    "If you'll press 'Okay' button, you'll be redirected to app's settings page, where you should select 'Allow all the time' for location permission";

  public string ServerIsNotRespondingDescription { get; } =
    "It seems like server is not responding.\n" +
    "Maybe internet connection is very unstable, or server address is invalid.\n\n" +
    "Please open the settings dialog and check if server address is typed correctly";

  public string IsInBackgroundDescription { get; } =
    "It seems like the app is in the background.\n" +
    "The map is not being shown in the background to prevent battery draining.";

  public string ShareButtonDescription { get; } = "Click to share the link to this room";
  public string MsgButtonDescription { get; } = "Click to set message to publish";
  public string GoToMyLocationButtonDescription { get; } = "Click to go to my location";
  public string StartPublishButtonDescription { get; } = "Click to start or stop publishing location";

}
