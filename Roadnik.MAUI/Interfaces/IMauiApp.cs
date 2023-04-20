using Grace.DependencyInjection;

namespace Roadnik.MAUI.Interfaces;

public interface IMauiApp
{
    IInjectionScope Container { get; }
}
