using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Data.Settings;

namespace Roadnik.Server.Interfaces;

internal interface ISettingsController
{
  IRxProperty<AppConfig?> Settings { get; }
}
