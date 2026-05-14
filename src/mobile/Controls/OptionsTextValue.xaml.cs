using Ax.Fw.Extensions;
using System.Windows.Input;

namespace Roadnik.MAUI.Controls;

public partial class OptionsTextValue : ContentView
{
  public OptionsTextValue()
  {
    InitializeComponent();
  }

  public static readonly BindableProperty TitleProperty = BindableProperty.Create(
    nameof(Title), 
    typeof(string), 
    typeof(OptionsTextValue), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsTextValue)_bindable;
      control.TitleLabel.Text = _new as string;
    });

  public static readonly BindableProperty DetailsTextProperty = BindableProperty.Create(
    nameof(DetailsText), 
    typeof(string), 
    typeof(OptionsTextValue), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsTextValue)_bindable;
      var text = _new as string;
      control.DetailsTextLabel.Text = text;
      control.DetailsTextLabel.IsVisible = !text.IsNullOrEmpty();
    });

  public static readonly BindableProperty ValueTextProperty = BindableProperty.Create(
    nameof(ValueText),
    typeof(string),
    typeof(OptionsTextValue),
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsTextValue)_bindable;
      var text = _new as string;
      control.ValueTextLabel.Text = text;
      control.ValueBorder.IsVisible = !text.IsNullOrEmpty();
    });

  public static readonly BindableProperty TapCommandProperty = BindableProperty.Create(
    nameof(TapCommand), 
    typeof(ICommand), 
    typeof(OptionsTextValue), 
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsTextValue)_bindable;
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

  public string ValueText
  {
    get => (string)GetValue(ValueTextProperty);
    set => SetValue(ValueTextProperty, value);
  }

  public static readonly BindableProperty RightEmojiProperty = BindableProperty.Create(
    nameof(RightEmoji),
    typeof(string),
    typeof(OptionsTextValue),
    propertyChanged: (_bindable, _old, _new) =>
    {
      var control = (OptionsTextValue)_bindable;
      var emoji = _new as string;
      var hasEmoji = !emoji.IsNullOrEmpty();
      control.RightEmojiLabel.Text = emoji;
      control.RightEmojiLabel.IsVisible = hasEmoji;
      if (hasEmoji)
        control.ValueBorder.IsVisible = false;
    });

  public string RightEmoji
  {
    get => (string)GetValue(RightEmojiProperty);
    set => SetValue(RightEmojiProperty, value);
  }

  public ICommand TapCommand
  {
    get => (ICommand)GetValue(TapCommandProperty);
    set => SetValue(TapCommandProperty, value);
  }

}