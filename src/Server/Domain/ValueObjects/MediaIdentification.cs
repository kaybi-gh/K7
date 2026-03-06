namespace K7.Server.Domain.ValueObjects;
public class MediaIdentification : ValueObject
{
    public string Title { get; set; }
    public DateOnly? ReleaseYear { get; set; }

    public MediaIdentification(string title)
    {
        Title = title;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Title;
        if (ReleaseYear.HasValue)
        {
            yield return ReleaseYear.Value;
        }
    }
}
