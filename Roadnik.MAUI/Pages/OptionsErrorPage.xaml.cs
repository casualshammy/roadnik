namespace Roadnik.MAUI.Pages;

public partial class OptionsErrorPage : ContentPage
{
	public OptionsErrorPage(
		string _description, 
		string _btnText)
	{
    Description = _description;
    ButtonText = _btnText;

    InitializeComponent();
    BindingContext = this;
	}

	public string Description { get; }
	public string ButtonText { get; }

  private async void OnButtonClicked(object _sender, EventArgs _e)
  {
    await Shell.Current.GoToAsync("//main/options");
		await Navigation.PopModalAsync(false);
  }
}