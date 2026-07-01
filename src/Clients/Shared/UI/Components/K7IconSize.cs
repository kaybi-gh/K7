namespace K7.Clients.Shared.UI.Components;

public enum K7IconSize
{
    Xs,
    Sm,
    Md,
    Lg,
    Xl,
}

public static class K7IconSizeExtensions
{
    public static string ToCssClass(this K7IconSize size) => size switch
    {
        K7IconSize.Xs => "k7-icon--xs",
        K7IconSize.Sm => "k7-icon--sm",
        K7IconSize.Md => "k7-icon--md",
        K7IconSize.Lg => "k7-icon--lg",
        K7IconSize.Xl => "k7-icon--xl",
        _ => "k7-icon--md",
    };
}
