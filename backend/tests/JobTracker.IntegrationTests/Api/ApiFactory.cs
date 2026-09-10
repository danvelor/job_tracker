using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobTracker.Modules.Jobs.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.IntegrationTests.Api;

public class ApiFactory(string connectionString, string environment = "Development")
    : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-tests-signing-key-at-least-32-bytes-long";
    public const string Issuer = "jobtracker-tests";
    public const string Audience = "jobtracker-tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("Jwt:Key", SigningKey);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);

        builder.UseSetting("Outbox:PollSeconds", "59");

        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter, LoopbackConnectionFilter>());
    }

    public async Task ResetSchemaAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync(
            "truncate jobs.jobs, jobs.job_photos, jobs.outbox_messages, jobs.notifications cascade");
    }

    public async Task<HttpClient> AuthenticatedClientAsync(Guid organizationId)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/dev-token", new { organizationId });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<DevToken>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed record DevToken(string Token);

    private sealed class LoopbackConnectionFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, following) =>
                {
                    context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                    await following();
                });

                next(app);
            };
    }
}
