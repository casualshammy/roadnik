using System.Runtime.CompilerServices;

namespace Roadnik.MAUI.Controls;

public class ModifiedCollectionView : CollectionView
{
  // 'IsEnabled' is not working - user can select item anyway.
  // Only need to do this hack for macOS+iOS+Android as this works as expected on Windows.
#if !WINDOWS
  protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    base.OnPropertyChanged(propertyName);

    if (propertyName == IsEnabledProperty.PropertyName)
    {
      if (IsEnabled)
      {
        InputTransparent = false;
        Opacity = 1.0;
      }
      else
      {
        InputTransparent = true;
        Opacity = 0.8;
      }
    }
  }
#endif
}
