namespace Roadnik.MAUI.Pages;

public partial class LocationPermissionPage : ContentPage
{
	public LocationPermissionPage()
	{
		InitializeComponent();
		BindingContext = this;
	}

  public string LocationPermissionDescription { get; } =
    "Our app requires access to your device's background location in order to provide you with accurate and up-to-date location tracking. " +
    "This means that even when our app is not shown on the screen, we're still able to keep track of your location in the background if you'll switch on the share function.\r\n\r\n" +
    "We understand that privacy is important to you, and we want to assure you that we take the protection of your personal data seriously. " +
    "Rest assured that we will only collect and use your location information in accordance with our privacy policy, " +
    "which is designed to protect your data and ensure that it is only used for the purposes you have explicitly consented to.\r\n\r\n" +
    "If you'll press 'Okay' button, you'll be redirected to app's settings page, where you should select 'Allow all the time' for location permission";
}