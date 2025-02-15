namespace Roadnik.MAUI.Controls;

public partial class OptionsHeader : ContentView
{
  public OptionsHeader()
  {
    InitializeComponent();
  }

  public static readonly BindableProperty TitleProperty = BindableProperty.Create(
    nameof(Title), 
    typeof(string), 
    typeof(OptionsHeader), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsHeader)_bindable;
      control.TitleLabel.Text = _new as string;
    });

  public string Title
  {
    get => (string)GetValue(TitleProperty);
    set => SetValue(TitleProperty, value);
  }

}