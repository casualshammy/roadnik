namespace Roadnik.MAUI.ViewModels;

internal class MainPageViewModel : BaseViewModel
{
  private Color p_startRecordButtonColor;
  private bool p_isSpinnerRequired;
  private string? p_webViewUrl;

  public MainPageViewModel()
  {
    p_startRecordButtonColor = Color.Parse("CornflowerBlue");
    p_isSpinnerRequired = true;
    p_webViewUrl = null;
  }

  public string Title { get; } = "Roadnik";
  public Color StartRecordButtonColor { get => p_startRecordButtonColor; set => SetProperty(ref p_startRecordButtonColor, value); }
  public bool IsSpinnerRequired { get => p_isSpinnerRequired; set => SetProperty(ref p_isSpinnerRequired, value); }
  public string? WebViewUrl { get => p_webViewUrl; set => SetProperty(ref p_webViewUrl, value); }

}
