using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Data.Settings;

namespace Roadnik.Server.Interfaces;

public interface ISettingsController
{
  IRxProperty<AppSettings?> Settings { get; }
}
