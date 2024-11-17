using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Common.Models;

public class EnumHashSetQueryParam<TEnum> : HashSet<TEnum> where TEnum : struct, Enum
{
    public static bool TryParse(string? value, out EnumHashSetQueryParam<TEnum>? enumHashSet)
    {
        // Format is "queryParameter=enum1,enum2,enum3"
        string[]? enumNames = value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hashSet = new EnumHashSetQueryParam<TEnum>();

        if (enumNames != null)
        {
            foreach (string enumName in enumNames)
            {
                if (Enum.TryParse(enumName, true, out TEnum enumValue))
                {
                    hashSet.Add(enumValue);
                }
                else
                {
                    throw new BadHttpRequestException($"Unable to parse query parameter value '{enumName}' for target enum '{typeof(TEnum).Name}'.");
                }
            }
            enumHashSet = hashSet;
            return true;
        }

        enumHashSet = null;
        return false;
    }
}
