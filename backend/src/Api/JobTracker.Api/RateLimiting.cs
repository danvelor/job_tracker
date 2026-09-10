using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using JobTracker.Api.Authentication;
using Microsoft.AspNetCore.RateLimiting;

namespace JobTracker.Api;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; init; } = true;
    public int PermitLimit { get; init; } = 100;
    public int WindowSeconds { get; init; } = 60;

    public int SegmentsPerWindow { get; init; } = 6;
}

internal static class RateLimiting
{
    public const string PolicyName = "per-tenant";

    public static IServiceCollection AddTenantRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                options.Enabled
                    ? RateLimitPartition.GetSlidingWindowLimiter(
                        PartitionKeyFor(context),
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = options.PermitLimit,
                            Window = TimeSpan.FromSeconds(options.WindowSeconds),
                            SegmentsPerWindow = options.SegmentsPerWindow,
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter("disabled"));

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter, out var metadata)
                    ? metadata
                    : TimeSpan.FromSeconds(options.WindowSeconds);

                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);

                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        title = "Too many requests",
                        status = StatusCodes.Status429TooManyRequests,
                        detail = "This organization has exceeded its request allowance.",
                        errorCode = "request.rate-limited",
                    },
                    cancellationToken);
            };
        });

        return services;
    }

    private static string PartitionKeyFor(HttpContext context)
    {
        var organization = context.User.FindFirstValue(TokenIssuer.OrganizationClaim);

        return organization is null
            ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
            : $"org:{organization}";
    }
}
