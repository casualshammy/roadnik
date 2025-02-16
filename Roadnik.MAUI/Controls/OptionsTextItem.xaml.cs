using System.Windows.Input;

namespace Roadnik.MAUI.Controls;

public partial class OptionsTextItem : ContentView
{
  public OptionsTextItem()
  {
    InitializeComponent();
  }

  public static readonly BindableProperty TitleProperty = BindableProperty.Create(
    nameof(Title), 
    typeof(string), 
    typeof(OptionsTextItem), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsTextItem)_bindable;
      control.TitleLabel.Text = _new as string;
    });

  public static readonly BindableProperty DetailsTextProperty = BindableProperty.Create(
    nameof(DetailsText), 
    typeof(string), 
    typeof(OptionsTextItem), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsTextItem)_bindable;
      control.DetailsTextLabel.Text = _new as string;
    });

  public static readonly BindableProperty TapCommandProperty = BindableProperty.Create(
    nameof(TapCommand), 
    typeof(ICommand), 
    typeof(OptionsTextItem), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsTextItem)_bindable;
      control.TapCommandHandler.Command = _new as ICommand;
    });

  public string Title
  {
    get => (string)GetValue(TitleProperty);
    set => SetValue(TitleProperty, value);
  }

  public string DetailsText
  {
    get=> (string)GetValue(DetailsTextProperty);
    set => SetValue(DetailsTextProperty, value);
  }

  public ICommand TapCommand
  {
    get => (ICommand)GetValue(TapCommandProperty);
    set => SetValue(TapCommandProperty, value);
  }

}