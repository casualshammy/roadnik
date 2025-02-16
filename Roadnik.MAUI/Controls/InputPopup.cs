namespace Roadnik.MAUI.Controls;

public class InputPopup : ContentPage
{
  public InputPopup()
  {
    Title = "Modal Dialog";

    var descriptionLabel = new Label
    {
      Text = "Please enter your text below:",
      HorizontalOptions = LayoutOptions.Center,
      VerticalOptions = LayoutOptions.CenterAndExpand
    };

    var entryField = new Entry
    {
      Placeholder = "Enter text here",
      HorizontalOptions = LayoutOptions.FillAndExpand,
      VerticalOptions = LayoutOptions.CenterAndExpand
    };

    var okButton = new Button
    {
      Text = "OK",
      HorizontalOptions = LayoutOptions.End,
      VerticalOptions = LayoutOptions.End
    };
    okButton.Clicked += async (sender, e) =>
    {
      // Handle OK button click
      await DisplayAlert("Info", "You clicked OK", "Close");
      await Navigation.PopModalAsync();
    };

    var cancelButton = new Button
    {
      Text = "Cancel",
      HorizontalOptions = LayoutOptions.Start,
      VerticalOptions = LayoutOptions.End
    };
    cancelButton.Clicked += async (sender, e) =>
    {
      // Handle Cancel button click
      await Navigation.PopModalAsync();
    };

    var buttonStack = new StackLayout
    {
      Orientation = StackOrientation.Horizontal,
      Children = { cancelButton, okButton }
    };

    var mainStack = new StackLayout
    {
      WidthRequest = 200,
      HeightRequest = 200,
      Padding = new Thickness(20),
      Children = { descriptionLabel, entryField, buttonStack }
    };

    Content = mainStack;
  }

}