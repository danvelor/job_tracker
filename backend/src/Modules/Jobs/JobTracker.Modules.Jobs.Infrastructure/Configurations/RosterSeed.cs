namespace JobTracker.Modules.Jobs.Infrastructure.Configurations;

internal static class RosterSeed
{
    public static readonly Guid DevelopmentOrganization =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid SecondOrganization =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid AssigneeOrtiz = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    public static readonly Guid AssigneeRuiz = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");
    public static readonly Guid AssigneeOther = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000003");

    public static readonly Guid CustomerAcme = Guid.Parse("cccccccc-0000-4000-8000-000000000001");
    public static readonly Guid CustomerBirch = Guid.Parse("cccccccc-0000-4000-8000-000000000002");
    public static readonly Guid CustomerOther = Guid.Parse("cccccccc-0000-4000-8000-000000000003");
}
