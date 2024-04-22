using System.Reactive;
using System.Text.Json.Serialization.Metadata;

namespace Roadnik.MAUI.Interfaces;

public interface IPreferencesStorage
{
  IObservable<Unit> PreferencesChanged { get; }
  T? GetValueOrDefault<T>(string _key);
  T? GetValueOrDefault<T>(string _key, JsonTypeInfo<T> _jsonTypeInfo);
  void RemoveValue(string _key);
  void SetValue<T>(string _key, T _value);
  void SetValue<T>(string _key, T _value, JsonTypeInfo<T> _jsonTypeInfo);
}
