namespace JobTracker.Common.Domain;

public interface ITenantScoped
{
    Guid OrganizationId { get; }
}
