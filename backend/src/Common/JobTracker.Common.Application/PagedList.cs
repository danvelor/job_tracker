namespace JobTracker.Common.Application;

/// <summary>
/// Cursor-shaped, not page-shaped: no Page, no PageSize and above all no
/// TotalCount. A total would need a second aggregate query per keystroke,
/// which is the cost NFR-5 rejects, and design A2 shows loaded rows rather
/// than matches. Assessment line 207 mandates the type name and says nothing
/// about its members.
/// </summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    public bool HasMore => NextCursor is not null;

    public static PagedList<T> Empty() => new([], null);
}
