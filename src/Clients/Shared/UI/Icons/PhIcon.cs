namespace K7.Clients.Shared.UI;

/// <summary>An icon reference combining a Phosphor variant and bare icon name. Converts to the full CSS class string for use with <see cref="K7Icon"/>.</summary>
public readonly record struct PhIcon(PhosphorVariant Variant, string Name)
{
    /// <summary>Creates a regular-variant icon from a plain icon name string.</summary>
    public static implicit operator PhIcon(string name) => new(PhosphorVariant.Regular, name);

    /// <summary>Returns the full CSS classes for the icon (e.g. "ph ph-house" or "ph-fill ph-house").</summary>
    public static implicit operator string(PhIcon icon)
    {
        var baseClass = icon.Variant switch
        {
            PhosphorVariant.Fill => "ph-fill",
            PhosphorVariant.Bold => "ph-bold",
            PhosphorVariant.Thin => "ph-thin",
            PhosphorVariant.Light => "ph-light",
            PhosphorVariant.Duotone => "ph-duotone",
            _ => "ph",
        };
        return $"{baseClass} ph-{icon.Name}";
    }
}

