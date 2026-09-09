using FluentValidation;
using JobTracker.Common.Application;
using JobTracker.Common.Application.Behaviors;
using JobTracker.Modules.Jobs.Application.Jobs.CreateJob;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Infrastructure.Notifications;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using JobTracker.Modules.Jobs.Infrastructure.Repositories;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

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
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationSender, LoggingNotificationSender>();
        services.AddScoped<IBackgroundQueue, HangfireBackgroundQueue>();
        services.AddScoped<MediatorJobRunner>();

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

        services.AddScoped<OutboxProcessor>();
        services.AddScoped<OutboxDrainJob>();

        // Hangfire keeps its own state in Postgres, in its own schema, so a
        // restart does not lose a scheduled send (architecture 4.4).
        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(postgres => postgres.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();

        return services;
    }

    /// <summary>
    /// Called once the application is built, because registering a recurring
    /// job needs the storage that AddHangfire only configures.
    ///
    /// Through IRecurringJobManager rather than the static RecurringJob, which
    /// reads JobStorage.Current — a process-wide singleton that is not set when
    /// the app is hosted by WebApplicationFactory. Hangfire's own error message
    /// recommends the service-based API, and taking the advice made the wiring
    /// testable as a side effect.
    /// </summary>
    public static void UseJobsModule(this IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<OutboxOptions>>().Value;

        services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OutboxDrainJob>(
            OutboxDrainJob.RecurringJobId,
            job => job.RunAsync(),
            $"*/{options.PollSeconds} * * * * *");
    }
}
