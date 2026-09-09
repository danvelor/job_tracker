namespace JobTracker.Modules.Jobs.Infrastructure.Configurations;

/// <summary>
/// The rosters have no write path (D-26) and administration is out of scope
/// (prd section 9), so a migration is the only way for them to exist. The
/// identifiers are fixed rather than generated: a smoke run, a screenshot and a
/// bug report all have to name the same assignee tomorrow as today.
///
/// The letter tells you what you are looking at without a join — `a…` is an
/// assignee, `c…` a customer — and the trailing digit says which.
/// </summary>
internal static class RosterSeed
{
    public static readonly Guid DevelopmentOrganization =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// A second organization with rows of its own. Without one, nothing in a
    /// running system would ever demonstrate that the tenant filter does
    /// anything at all.
    /// </summary>
    public static readonly Guid SecondOrganization =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid AssigneeOrtiz = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    public static readonly Guid AssigneeRuiz = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");
    public static readonly Guid AssigneeOther = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000003");

    public static readonly Guid CustomerAcme = Guid.Parse("cccccccc-0000-4000-8000-000000000001");
    public static readonly Guid CustomerBirch = Guid.Parse("cccccccc-0000-4000-8000-000000000002");
    public static readonly Guid CustomerOther = Guid.Parse("cccccccc-0000-4000-8000-000000000003");
}
