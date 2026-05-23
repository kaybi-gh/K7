using System.Collections;
using System.Reflection;
using K7.Server.Domain.Common;

namespace K7.Server.Application.Features.Notifications.Services;

public class NotificationEventDataSerializer
{
    public IReadOnlyDictionary<string, object?> Serialize(BaseEvent domainEvent)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var eventType = domainEvent.GetType();

        data["EventType"] = eventType.Name;

        FlattenObject(domainEvent, "", data, depth: 0);

        return data;
    }

    private static void FlattenObject(object obj, string prefix, Dictionary<string, object?> data, int depth)
    {
        if (depth > 3)
            return;

        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !IsIgnoredProperty(p));

        foreach (var prop in properties)
        {
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            var value = prop.GetValue(obj);

            if (value is null)
            {
                data[key] = null;
            }
            else if (IsSimpleType(prop.PropertyType))
            {
                data[key] = value;
            }
            else if (value is IEnumerable enumerable and not string)
            {
                var count = 0;
                foreach (var _ in enumerable)
                    count++;
                data[$"{key}.Count"] = count;
            }
            else
            {
                FlattenObject(value, key, data, depth + 1);
            }
        }
    }

    private static bool IsSimpleType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
               || underlying.IsEnum
               || underlying == typeof(string)
               || underlying == typeof(decimal)
               || underlying == typeof(DateTime)
               || underlying == typeof(DateTimeOffset)
               || underlying == typeof(DateOnly)
               || underlying == typeof(TimeOnly)
               || underlying == typeof(TimeSpan)
               || underlying == typeof(Guid);
    }

    private static bool IsIgnoredProperty(PropertyInfo prop)
    {
        return prop.Name is "DomainEvents" or "Id"
               || prop.GetIndexParameters().Length > 0;
    }
}
