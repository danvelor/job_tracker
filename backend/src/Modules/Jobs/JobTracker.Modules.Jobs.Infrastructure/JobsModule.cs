using FluentValidation;
using JobTracker.Common.Application;
using JobTracker.Common.Application.Behaviors;
using JobTracker.Common.Infrastructure;
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
            .AddInterceptors(new InsertOutboxMessagesInterceptor()));

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IPartyRepository, PartyRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationSender, LoggingNotificationSender>();
        services.AddScoped<IBackgroundQueue, HangfireBackgroundQueue>();
        services.AddScoped<MediatorJobRunner>();
        services.AddScoped<IEventBus, EventBus>();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(CreateJobCommand).Assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(
            typeof(CreateJobCommand).Assembly, includeInternalTypes: true);

        services.AddScoped<OutboxProcessor>();
        services.AddScoped<OutboxDrainJob>();

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(postgres => postgres.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();

        return services;
    }

    public static void UseJobsModule(this IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<OutboxOptions>>().Value;

        services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OutboxDrainJob>(
            OutboxDrainJob.RecurringJobId,
            job => job.RunAsync(),
            $"*/{options.PollSeconds} * * * * *");
    }
}
