namespace JobTracker.Common.Application;

public sealed record PagedList<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    public bool HasMore => NextCursor is not null;

    public static PagedList<T> Empty() => new([], null);
}
