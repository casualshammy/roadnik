using CommunityToolkit.Maui.Views;

namespace Roadnik.MAUI.Controls;

public partial class ImagePopup : Popup
{
	public ImagePopup(byte[] _pngBytes, int _width = 200, int _height = 200)
	{
		InitializeComponent();

		p_image.WidthRequest = _width;
		p_image.HeightRequest = _height;
    p_image.Source = ImageSource.FromStream(() => new MemoryStream(_pngBytes));
  }

}