namespace K7.Clients.Shared.UI;

/// <summary>Flag icon CSS class constants. Use <c>Flags.France</c> with K7Icon or as a CSS class directly.</summary>
public static class Flags
{
    public const string France = "fi fi-fr";
    public const string UnitedKingdom = "fi fi-gb";
    public const string Germany = "fi fi-de";
    public const string Spain = "fi fi-es";
    public const string Italy = "fi fi-it";
    public const string Japan = "fi fi-jp";
    public const string SouthKorea = "fi fi-kr";
    public const string Brazil = "fi fi-br";
    public const string Russia = "fi fi-ru";
    public const string China = "fi fi-cn";
    public const string SaudiArabia = "fi fi-sa";
    public const string Netherlands = "fi fi-nl";
    public const string Poland = "fi fi-pl";
    public const string Sweden = "fi fi-se";
    public const string Denmark = "fi fi-dk";
    public const string Finland = "fi fi-fi";
    public const string Norway = "fi fi-no";
    public const string Turkey = "fi fi-tr";
    public const string Ukraine = "fi fi-ua";
    public const string India = "fi fi-in";
    public const string Thailand = "fi fi-th";
    public const string Vietnam = "fi fi-vn";
    public const string Greece = "fi fi-gr";
    public const string Israel = "fi fi-il";
    public const string Czechia = "fi fi-cz";
    public const string Hungary = "fi fi-hu";
    public const string Romania = "fi fi-ro";
    public const string Indonesia = "fi fi-id";

    /// <summary>Returns the flag CSS class for a given ISO 3166-1 alpha-2 country code (lowercase).</summary>
    public static string FromCountryCode(string countryCode) => $"fi fi-{countryCode}";
}
