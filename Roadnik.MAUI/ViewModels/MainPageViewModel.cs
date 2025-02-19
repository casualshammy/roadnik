namespace Roadnik.MAUI.ViewModels;

internal class MainPageViewModel : BaseViewModel
{
  private bool p_isSpinnerRequired;
  private string? p_webViewUrl;
  private bool p_isInBackground;
  private string p_title;

  public MainPageViewModel()
  {
    p_isSpinnerRequired = true;
    p_webViewUrl = null;
    p_isInBackground = false;
    p_title = "Roadnik";
  }

  public string Title { get => p_title; set => SetProperty(ref p_title, value); }
  public bool IsSpinnerRequired { get => p_isSpinnerRequired; set => SetProperty(ref p_isSpinnerRequired, value); }
  public string? WebViewUrl { get => p_webViewUrl; set => SetProperty(ref p_webViewUrl, value); }

  public bool IsInBackground
  {
    get => p_isInBackground;
    set
    {
      SetProperty(ref p_isInBackground, value);
      OnPropertyChanged(nameof(CanShowRegilarView));
    }
  }

  public bool CanShowRegilarView => !p_isInBackground;

  public string IsInBackgroundDescription { get; } =
    "It seems like the app is in the background.\n" +
    "The map is not being shown in the background to prevent battery draining.";

  public string ShareButtonDescription { get; } = "Click to share the link to this room";
  public string GoToMyLocationButtonDescription { get; } = "Click to go to my location";
  public string StartPublishButtonDescription { get; } = "Click to start or stop publishing location";
  public string OpenFlyoutButtonDescription { get; } = "Click to open flyout menu";

}
