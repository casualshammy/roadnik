using System.Windows.Input;

namespace Roadnik.MAUI.Controls;

public partial class OptionsSwitch : ContentView
{
  public OptionsSwitch()
  {
    InitializeComponent();
  }

  public static readonly BindableProperty TitleProperty = BindableProperty.Create(
    nameof(Title), 
    typeof(string), 
    typeof(OptionsSwitch), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsSwitch)_bindable;
      control.TitleLabel.Text = _new as string;
    });

  public static readonly BindableProperty DetailsTextProperty = BindableProperty.Create(
    nameof(DetailsText), 
    typeof(string), 
    typeof(OptionsSwitch), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsSwitch)_bindable;
      control.DetailsTextLabel.Text = _new as string;
    });

  public static readonly BindableProperty TapCommandProperty = BindableProperty.Create(
    nameof(TapCommand), 
    typeof(ICommand), 
    typeof(OptionsSwitch), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsSwitch)_bindable;
      control.TapCommandHandler.Command = _new as ICommand;
    });

  public static readonly BindableProperty SwitchIsToggledProperty = BindableProperty.Create(
    nameof(SwitchIsToggled),
    typeof(bool),
    typeof(OptionsSwitch),
    defaultValue: false,
    defaultBindingMode: BindingMode.TwoWay,
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsSwitch)_bindable;
      if (control.Switch.IsToggled == (bool)_new)
        return;

      control.Switch.IsToggled = (bool)_new;
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

  public bool SwitchIsToggled
  {
    get => (bool)GetValue(SwitchIsToggledProperty);
    set => SetValue(SwitchIsToggledProperty, value);
  }

  private void Switch_Toggled(object _sender, ToggledEventArgs _e)
  {
    if (SwitchIsToggled != _e.Value)
      SwitchIsToggled = _e.Value;
  }

}