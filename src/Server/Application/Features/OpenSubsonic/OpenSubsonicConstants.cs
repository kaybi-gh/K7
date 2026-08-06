namespace K7.Server.Application.Features.OpenSubsonic;

public static class OpenSubsonicConstants
{
    public const string ProtocolVersion = "1.16.1";
    public const string ServerType = "K7";
    public const string HelpUrl = "/settings/external-clients";

    public const int ErrorGeneric = 0;
    public const int ErrorRequiredParam = 10;
    public const int ErrorClientIncompatible = 20;
    public const int ErrorServerIncompatible = 30;
    public const int ErrorWrongCredentials = 40;
    public const int ErrorTokenUnsupported = 41;
    public const int ErrorNotAuthenticated = 42;
    public const int ErrorAuthConflict = 43;
    public const int ErrorInvalidApiKey = 44;
    public const int ErrorUnauthorized = 50;
    public const int ErrorNotFound = 70;

    /// <summary>OpenSubsonic starred maps to a UserRating above this threshold.</summary>
    public const double StarredThreshold = 5;

    /// <summary>OpenSubsonic rating (1-5) maps to K7 UserRating via multiply by this factor.</summary>
    public const int RatingScaleFactor = 2;
}
