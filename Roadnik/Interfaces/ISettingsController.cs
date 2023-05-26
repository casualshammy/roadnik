using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Modules.Settings;

namespace Roadnik.Interfaces;

internal interface ISettingsController
{
    IRxProperty<SettingsImpl?> Value { get; }
}
