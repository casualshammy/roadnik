using Roadnik.MAUI.Data.PageConsts;

namespace Roadnik.MAUI.ViewModels;

internal class MainPageViewModel : BaseViewModel
{
  private bool p_isSpinnerRequired;
  private string? p_webViewUrl;
  private string p_title;
  private string p_locationBtnImage;
  private bool p_isDiscordButtonVisible;
  private Brush p_discordButtonColor;
  private string? p_discordStatusText;
  private double p_discordBtnOpacity = MainPageConsts.MAP_SIDE_BTN_COLLAPSED_OPACITY;
  private Rect p_discordBtnBounds = new(10, 120, 44, 44);

  public MainPageViewModel()
  {
    p_isSpinnerRequired = true;
    p_webViewUrl = null;
    p_title = "Roadnik";
    p_locationBtnImage = "location_empty.svg";
    p_isDiscordButtonVisible = false;
    p_discordButtonColor = Brush.Black;
    p_discordStatusText = null;
  }

  public string Title { get => p_title; set => SetProperty(ref p_title, value); }
  public bool IsSpinnerRequired { get => p_isSpinnerRequired; set => SetProperty(ref p_isSpinnerRequired, value); }
  public string? WebViewUrl { get => p_webViewUrl; set => SetProperty(ref p_webViewUrl, value); }
  public string LocationBtnImage { get => p_locationBtnImage; set => SetProperty(ref p_locationBtnImage, value); }
  public bool IsDiscordButtonVisible { get => p_isDiscordButtonVisible; set => SetProperty(ref p_isDiscordButtonVisible, value); }
  public Brush DiscordBtnColor { get => p_discordButtonColor; set => SetProperty(ref p_discordButtonColor, value); }
  public string? DiscordStatusText { get => p_discordStatusText; set => SetProperty(ref p_discordStatusText, value); }
  public double DiscordBtnOpacity { get => p_discordBtnOpacity; set => SetProperty(ref p_discordBtnOpacity, value); }
  public Rect DiscordBtnBounds { get => p_discordBtnBounds; set => SetProperty(ref p_discordBtnBounds, value); }

  public string ShareButtonDescription { get; } = "Click to share the link to this room";
  public string GoToMyLocationButtonDescription { get; } = "Click to go to my location";
  public string StartPublishButtonDescription { get; } = "Click to start or stop publishing location";
  public string OpenFlyoutButtonDescription { get; } = "Click to open flyout menu";
  public string DiscordToggleButtonDescription { get; } = "Click to toggle Discord integration";

}
