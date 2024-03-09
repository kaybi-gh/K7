using System;
namespace MediaServer.Domain.ValueObjects;
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
        yield return ReleaseYear!;
    }
}
