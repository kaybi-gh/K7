using Microsoft.EntityFrameworkCore;

namespace K7.Clients.MAUI.Services.Authentication;

public class OpenIddictDbContext(DbContextOptions<OpenIddictDbContext> options) : DbContext(options);
