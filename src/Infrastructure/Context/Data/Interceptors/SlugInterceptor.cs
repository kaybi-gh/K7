using System.Text.RegularExpressions;
using MediaServer.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MediaServer.Infrastructure.Context.Data.Interceptors;

public partial class SlugInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries<BaseSlugEntity>())
        {
            if (entry.State == EntityState.Added ||
                entry.State == EntityState.Modified && entry.Entity.Slug != GenerateSlug(entry.Entity.GetSlugSource()))
            {
                var entityType = entry.Entity.GetType();
                var method = typeof(SlugInterceptor).GetMethod(nameof(GenerateUniqueSlug))!.MakeGenericMethod(entityType);
                entry.Entity.Slug = (string)method.Invoke(this, [entry.Entity.GetSlugSource(), context])!;
            }
        }
    }

    public string GenerateUniqueSlug<T>(string input, DbContext context) where T : BaseSlugEntity
    {
        string slug = GenerateSlug(input);

        // Add auto-incrementing suffix if the generated slug already exists in the database
        int suffix = 1;
        string uniqueSlug = slug;
        var dbSet = context.Set<T>();
        while (dbSet.Any(e => e.Slug == uniqueSlug))
        {
            uniqueSlug = $"{slug}-{suffix}";
            suffix++;
        }

        return uniqueSlug;
    }

    public static string GenerateSlug(string input)
    {
        var slug = input.ToLower();
        slug = NonAlphanumericCharactersExpression().Replace(slug, "-");
        slug = slug.Trim('-');
        return slug;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumericCharactersExpression();
}

