namespace Roadnik.MAUI.ViewModels;

internal class MainPageViewModel : BaseViewModel
{
  private bool p_isSpinnerRequired;
  private string? p_webViewUrl;
  private string p_title;
  private string p_locationBtnImage;

  public MainPageViewModel()
  {
    p_isSpinnerRequired = true;
    p_webViewUrl = null;
    p_title = "Roadnik";
    p_locationBtnImage = "location_empty.svg";
  }

  public string Title { get => p_title; set => SetProperty(ref p_title, value); }
  public bool IsSpinnerRequired { get => p_isSpinnerRequired; set => SetProperty(ref p_isSpinnerRequired, value); }
  public string? WebViewUrl { get => p_webViewUrl; set => SetProperty(ref p_webViewUrl, value); }
  public string LocationBtnImage { get => p_locationBtnImage; set => SetProperty(ref p_locationBtnImage, value); }

  public string ShareButtonDescription { get; } = "Click to share the link to this room";
  public string GoToMyLocationButtonDescription { get; } = "Click to go to my location";
  public string StartPublishButtonDescription { get; } = "Click to start or stop publishing location";
  public string OpenFlyoutButtonDescription { get; } = "Click to open flyout menu";

}
