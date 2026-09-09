using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobTracker.Modules.Jobs.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.IntegrationTests.Api;

/// <summary>
/// The real application, over the real database, reached through the real HTTP
/// pipeline. Nothing is substituted except the connection string and the
/// environment name — a factory that swapped the authentication handler for a
/// permissive one would stop testing the thing most worth testing.
/// </summary>
public sealed class ApiFactory(string connectionString, string environment = "Development")
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
    }

    /// <summary>
    /// Drops and re-migrates through the application's own DbContext
    /// registration, so a test starts from a clean schema without knowing how
    /// the host wired it.
    /// </summary>
    public async Task ResetSchemaAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
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
}
