using System;
namespace MediaServer.Domain.ValueObjects;
public class MovieIdentification : ValueObject
{
    public string Title { get; set; }
    public DateOnly? ReleaseYear { get; set; }

    public MovieIdentification(string title)
    {
        Title = title;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Title;
        yield return ReleaseYear!;
    }
}
