using System.Text;
using JobTracker.Api;
using JobTracker.Api.Authentication;
using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Billing.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Presentation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
          ?? throw new InvalidOperationException("The Jwt configuration section is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        // No clock skew. The default five minutes means an expired token keeps
        // working for five more, which is five minutes of a revoked session.
        ClockSkew = TimeSpan.Zero,
    });

// Every route requires an authenticated caller unless it opts out. The
// framework default runs the other way, and one forgotten attribute is an open
// route nobody notices until it is read.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHttpContextAccessor();
// One scoped instance behind two interfaces: a request reads its claim, and
// the outbox drain sets the tenant from the message it is processing.
builder.Services.AddScoped<HttpTenantContext>();
builder.Services.AddScoped<ITenantContext>(services => services.GetRequiredService<HttpTenantContext>());
builder.Services.AddScoped<ITenantContextSetter>(services => services.GetRequiredService<HttpTenantContext>());
builder.Services.AddSingleton<TokenIssuer>();

builder.Services.AddJobsModule(
    builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required."),
    builder.Configuration);

builder.Services.AddBillingModule(
    builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required."),
    builder.Configuration);

builder.Services.AddSingleton<DatabaseMigrator>();
builder.Services.Configure<RateLimitingOptions>(
    builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.AddTenantRateLimiting(builder.Configuration);

builder.Services.AddEndpoints(JobsPresentation.Assembly);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// After authentication, because the partition key is the org claim and a
// limiter running before it would put every request in the anonymous bucket.
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    // AllowAnonymous on both. The fallback policy applies to a request that
    // selects no endpoint, so without it the pipeline refuses these before
    // their own middleware sees them — which is what happened, and what a
    // README promising the URLs is what caught.
    app.MapOpenApi().AllowAnonymous();
    app.MapDevToken();

    // Architecture 7.5. Two conditions, not one: registered only here, and
    // reachable only from loopback. ASPNETCORE_ENVIRONMENT is a string in a
    // Compose file and its failure mode is silent, so the filter is the
    // condition that does not depend on somebody getting that right.
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new LocalOnlyDashboardFilter()],
        // The dashboard is for reading. A reviewer who can requeue a job from
        // it can also produce a duplicate invoice by hand, which would be a
        // confusing thing to discover in the data.
        IsReadOnlyFunc = _ => true,
    })
        // Mapped as an endpoint and marked anonymous, rather than middleware
        // ahead of the pipeline. The dashboard cannot use bearer auth — a
        // browser sends no Authorization header — so its guards are the two
        // above: Development only, and loopback only.
        .AllowAnonymous();
}

app.MapEndpoints();

// Anonymous and unmetered: Compose polls it before the container has any
// credentials, and a healthcheck that needed a token would never turn the
// container healthy.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    // Compose polls this every few seconds. A limiter that counted those would
    // eventually mark the container unhealthy under its own healthcheck — the
    // system failing because it was watching itself.
    .DisableRateLimiting()
    .WithTags("Diagnostics");

// Before the first request is served, and only when configuration asks. A test
// host that migrated on every start would fight its own fixture.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.GetRequiredService<DatabaseMigrator>().MigrateAsync(default);
}

app.Services.UseJobsModule();

app.Run();

/// <summary>
/// WebApplicationFactory needs a nameable type, and top-level statements do not
/// produce a public one.
/// </summary>
public partial class Program;
