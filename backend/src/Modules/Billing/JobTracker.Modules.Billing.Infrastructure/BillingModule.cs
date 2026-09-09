using JobTracker.Common.Application;
using JobTracker.Modules.Billing.Application;
using JobTracker.Modules.Billing.Domain;
using JobTracker.Modules.Billing.Infrastructure.Repositories;
using JobTracker.Modules.Jobs.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Modules.Billing.Infrastructure;

/// <summary>
/// The second module, and the proof that the first's registration was a shape
/// rather than a one-off: one method, one schema, one migration history, and
/// nothing shared by accident.
/// </summary>
public static class BillingModule
{
    public static IServiceCollection AddBillingModule(
        this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));

        services.AddDbContext<BillingDbContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

        // Its own unit of work over its own context. Sharing Jobs' would make
        // one SaveChanges write both schemas, which is the coupling the
        // boundary exists to prevent — and a keyed registration of the shared
        // interface let the handler resolve Jobs' by asking for the unkeyed
        // one, which compiled and wrote nothing.
        services.AddScoped<IBillingUnitOfWork, BillingUnitOfWork>();

        services.AddScoped<
            IIntegrationEventHandler<JobCompletedIntegrationEvent>,
            GenerateInvoiceOnJobCompletedHandler>();

        return services;
    }
}

public sealed class BillingUnitOfWork(BillingDbContext context) : IBillingUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
