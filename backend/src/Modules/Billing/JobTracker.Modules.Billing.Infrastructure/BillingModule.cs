using JobTracker.Common.Application;
using JobTracker.Modules.Billing.Application;
using JobTracker.Modules.Billing.Domain;
using JobTracker.Modules.Billing.Infrastructure.Repositories;
using JobTracker.Modules.Jobs.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Modules.Billing.Infrastructure;

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
