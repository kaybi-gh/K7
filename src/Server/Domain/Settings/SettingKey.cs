namespace K7.Server.Domain.Settings;

public interface ISettingKey
{
    string Name { get; }
    Type ValueType { get; }
    object? BoxedDefaultValue { get; }
}

public sealed class SettingKey<T>(string name, T? defaultValue = default) : ISettingKey
{
    public string Name { get; } = name;
    public T? DefaultValue { get; } = defaultValue;
    public Type ValueType => typeof(T);
    public object? BoxedDefaultValue => DefaultValue;
}
