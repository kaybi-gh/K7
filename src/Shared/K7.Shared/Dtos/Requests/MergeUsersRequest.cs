namespace K7.Shared.Dtos.Requests;

public sealed record MergeUsersRequest
{
    public MergeStrategy? Strategy { get; init; }
}
