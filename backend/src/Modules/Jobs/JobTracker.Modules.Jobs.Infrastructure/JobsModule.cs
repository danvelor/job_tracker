using FluentValidation;
using JobTracker.Common.Application;
using JobTracker.Common.Application.Behaviors;
using JobTracker.Modules.Jobs.Application.Jobs.CreateJob;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using JobTracker.Modules.Jobs.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Modules.Jobs.Infrastructure;

/// <summary>
/// Everything the Jobs module needs, in one method the composition root calls.
///
/// It lives inside the module rather than in the API for the reason the
/// repositories are internal: the host should not be able to name a
/// JobRepository, and it does not have to. Adding a repository is a change in
/// this file alone, and a second module is a second call.
/// </summary>
public static class JobsModule
{
    public static IServiceCollection AddJobsModule(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<JobsDbContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", JobsDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            // Architecture 4.2. Registered on the context rather than called
            // by a handler, so no handler can forget and no code path can
            // change state without its consequences being recorded.
            .AddInterceptors(new InsertOutboxMessagesInterceptor()));

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IPartyRepository, PartyRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(CreateJobCommand).Assembly);
            // The module's pipeline, not the host's.
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // includeInternalTypes is not decoration: validators are internal by
        // architecture 9.1, and without it FluentValidation finds none and
        // every invalid request answers 201.
        services.AddValidatorsFromAssembly(
            typeof(CreateJobCommand).Assembly, includeInternalTypes: true);

        return services;
    }
}
