namespace K7.Clients.Shared.UI.Helpers;

public static class ByteSizeFormatter
{
    private const long Mb = 1024L * 1024;
    private const long Gb = 1024L * 1024 * 1024;

    public static string Format(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < Mb => $"{bytes / 1024.0:F1} KB",
        < Gb => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    public static (double amount, string unit) SplitForEdit(long bytes)
    {
        var gb = bytes / (double)Gb;
        if (gb >= 1 && Math.Abs(gb - Math.Round(gb, 2)) < 0.001)
            return (Math.Round(gb, 2), "GB");

        return (Math.Round(bytes / (double)Mb, 1), "MB");
    }

    public static long ToBytes(double amount, string unit) =>
        unit == "GB" ? (long)Math.Round(amount * Gb) : (long)Math.Round(amount * Mb);
}
