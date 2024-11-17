using MudBlazor;

namespace K7.Clients.Shared.Domain.Models
{
    public class ThemeWrapper(string name, MudTheme theme)
    {
        public string Name { get; set; } = name;
        public MudTheme Theme { get; set; } = theme;
    }
}
