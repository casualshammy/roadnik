using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;

namespace Roadnik.MAUI.Pages;

public partial class BleDevicesPage : CContentPage
{
  public BleDevicesPage()
  {
    InitializeComponent();
    BindingContext = new BleDevicesPageViewModel();
  }
}
