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

    /// <summary>
    /// Segments per window. More segments means the window slides more
    /// smoothly; the cost is bookkeeping per partition, and six is enough that
    /// a caller never waits a full minute for capacity that expired seconds ago.
    /// </summary>
    public int SegmentsPerWindow { get; init; } = 6;
}

internal static class RateLimiting
{
    public const string PolicyName = "per-tenant";

    /// <summary>
    /// Architecture 7.4. A sliding window rather than a fixed one: a fixed
    /// window lets a caller spend a whole allowance at the end of one window
    /// and another at the start of the next, which is twice the limit in an
    /// instant — the burst the limit exists to prevent.
    ///
    /// Partitioned by the <c>org</c> claim, falling back to remote address for
    /// callers who have not authenticated yet. A single global bucket would let
    /// the loudest organization set everyone else's availability, which is a
    /// multi-tenancy failure wearing a performance costume.
    /// </summary>
    public static IServiceCollection AddTenantRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global, with an explicit exemption for the healthcheck, rather
            // than a policy each endpoint opts into. The default runs the safe
            // way and one forgotten attribute leaves a route metered rather
            // than open — the same reasoning as the fallback authorization
            // policy.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                options.Enabled
                    ? RateLimitPartition.GetSlidingWindowLimiter(
                        PartitionKeyFor(context),
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = options.PermitLimit,
                            Window = TimeSpan.FromSeconds(options.WindowSeconds),
                            SegmentsPerWindow = options.SegmentsPerWindow,
                            // No queue. A caller over its limit is told so at
                            // once rather than held on a socket until capacity
                            // appears — holding it consumes the very resource
                            // the limit protects.
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter("disabled"));

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                // Without this a client has to guess how long to wait, and
                // clients guess badly — usually by retrying at once, which is
                // the behaviour the limit exists to stop.
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
            // No claim to partition by. Address is a poor key — a proxy makes
            // every caller look like one — but it is better than a single
            // shared bucket, where one anonymous caller locks out the token
            // endpoint for everybody.
            ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
            : $"org:{organization}";
    }
}
