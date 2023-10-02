using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Modules.Settings;

namespace Roadnik.Interfaces;

public interface ISettingsController
{
    IRxProperty<SettingsImpl?> Settings { get; }
}
