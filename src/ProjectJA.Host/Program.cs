// SPDX-License-Identifier: BUSL-1.1
using Coravel;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using ProjectJA.Host.Authentication;
using ProjectJA.Host.Components;
using StackExchange.Redis;
using ProjectJA.Host.Endpoints;
using ProjectJA.Host.Hubs;
using ProjectJA.Infrastructure;
using ProjectJA.Infrastructure.Email;
using ProjectJA.Infrastructure.Tenancy;
using ProjectJA.Modules.Audit.Application;
using ProjectJA.SharedKernel.Realtime;
using Scalar.AspNetCore;
using ProjectJA.Modules.Audit;
using ProjectJA.Modules.Boards;
using ProjectJA.Modules.Identity;
using ProjectJA.Modules.Issues;
using ProjectJA.Modules.Notifications;
using ProjectJA.Modules.Projects;
using ProjectJA.Modules.Search;
using ProjectJA.Modules.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, sp, logger) =>
{
    logger.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Application", "ProjectJA")
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
        .WriteTo.Console(outputTemplate:
            "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.File(
            path: builder.Configuration["Serilog:FilePath"] ?? "logs/projectja-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}");
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<ProjectJA.Host.OpenApi.SecuritySchemeDocumentTransformer>());

// OpenTelemetry: traces + metrics. OTLP export is wired only when configured.
var otlpEndpoint = builder.Configuration["OpenTelemetry:Otlp:Endpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "ProjectJA",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Npgsql")
            .AddSource("ProjectJA.*");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            m.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    });

var primaryConnection = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Database=projectja;Username=postgres;Password=postgres";
builder.Services.AddHealthChecks()
    .AddNpgSql(primaryConnection, name: "postgres", tags: ["ready"]);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddIdentityModule()
    .AddProjectsModule()
    .AddIssuesModule()
    .AddWorkflowsModule()
    .AddBoardsModule()
    .AddSearchModule()
    .AddNotificationsModule()
    .AddAuditModule();

var oidcOptions = builder.Configuration.GetSection(OidcAuthenticationOptions.SectionName).Get<OidcAuthenticationOptions>() ?? new();
builder.Services.AddSingleton(oidcOptions);

var authBuilder = builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme);
authBuilder.AddIdentityCookies();

if (oidcOptions.Enabled
    && !string.IsNullOrWhiteSpace(oidcOptions.Authority)
    && !string.IsNullOrWhiteSpace(oidcOptions.ClientId))
{
    authBuilder.AddOpenIdConnect(OidcAuthenticationOptions.Scheme, oidcOptions.DisplayName, opts =>
    {
        opts.SignInScheme = IdentityConstants.ExternalScheme;
        opts.Authority = oidcOptions.Authority;
        opts.ClientId = oidcOptions.ClientId;
        opts.ClientSecret = oidcOptions.ClientSecret;
        opts.ResponseType = OpenIdConnectResponseType.Code;
        opts.UsePkce = true;
        opts.SaveTokens = true;
        opts.GetClaimsFromUserInfoEndpoint = true;
        opts.Scope.Clear();
        opts.Scope.Add("openid");
        opts.Scope.Add("email");
        opts.Scope.Add("profile");
        foreach (var s in oidcOptions.Scopes) opts.Scope.Add(s);
    });
}

builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/login";
    opts.LogoutPath = "/logout";
    opts.AccessDeniedPath = "/access-denied";

    // Cookie-auth's default Challenge is a 302 to LoginPath — great for
    // browsers hitting protected pages, wrong for REST callers (who want
    // 401 / 403 they can act on). Branch on path: /api/* gets the API
    // answer, everything else keeps the browser-friendly redirect. Closes
    // the long-standing 302-vs-401 asymmetry between anonymous GETs and
    // anonymous mutating verbs.
    opts.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    opts.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(opts =>
{
    // Tenant-wide administrator policy. Read off the `is_org_admin` claim
    // stamped at sign-in by TenantStampedClaimsFactory. Gates /admin/*,
    // /audit, /system/versioning, /invites, /admin/users + their REST
    // counterparts. Project-level role checks (ProjectAccess) are
    // independent and stay as-is.
    opts.AddPolicy("OrgAdmin", p => p.RequireAuthenticatedUser().RequireClaim("is_org_admin", "true"));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<InstanceIdentity>();

var redisConnection = builder.Configuration.GetConnectionString("Redis")
                      ?? builder.Configuration["Redis:ConnectionString"];
var signalR = builder.Services.AddSignalR();
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        _ => ConnectionMultiplexer.Connect(redisConnection));
    signalR.AddStackExchangeRedis(redisConnection,
        options => options.Configuration.ChannelPrefix = RedisChannel.Literal("projectja:signalr"));
    builder.Services.AddHostedService<RedisBoardEventBridge>();
}

builder.Services.AddSingleton<BoardEventStream>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

builder.Services.AddMudServices();

// Per-user theming: immutable catalog (singleton) + per-circuit selection (scoped).
builder.Services.AddSingleton<ProjectJA.Host.Theming.ThemeCatalog>();
builder.Services.AddSingleton<ProjectJA.Host.Theming.AvatarCatalog>();
builder.Services.AddScoped<ProjectJA.Host.Theming.ThemeState>();
builder.Services.AddScoped<ProjectJA.Host.Theming.AvatarState>();

builder.Services.AddScheduler();

var app = builder.Build();

// Force-load ClosedXML at startup so /system/versioning lists it. The Excel
// export only touches the type lazily on first request, which means the
// diagnostic page hides the dependency until someone actually exports.
_ = typeof(ClosedXML.Excel.XLWorkbook);

app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0}ms";
    opts.EnrichDiagnosticContext = (diagnosticContext, http) =>
    {
        if (http.Items.TryGetValue("Tenant.Slug", out var slug) && slug is string s)
            diagnosticContext.Set("TenantSlug", s);
    };
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Tenant resolution must run BEFORE authentication on every request: ASP.NET Identity's
// cookie validator reloads the user from AppDbContext on every request (including for
// static assets like /_framework/blazor.web.js), and that needs the tenant's connection
// string. Resolving the default tenant is cheap (cached after the first hit).
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapIdentityEndpoints();
app.MapProjectsEndpoints();
app.MapIssuesEndpoints();
app.MapWorkflowsEndpoints();
app.MapBoardsEndpoints();
app.MapSearchEndpoints();
app.MapNotificationsEndpoints();
app.MapAuditEndpoints();

app.MapHub<BoardHub>("/hubs/board");
app.MapAuthEndpoints();

app.MapHealthChecks("/healthz",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false, // liveness: process is up, no dependencies tested
    });
app.MapHealthChecks("/healthz/ready",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"), // readiness: DB reachable
    });

app.MapOpenApi();              // /openapi/v1.json
app.MapScalarApiReference();   // /scalar/v1 — interactive UI

if (app.Configuration.GetValue<bool>("Bootstrap:Enabled", true))
    await StartupBootstrap.EnsureSeededAsync(app.Services, app.Configuration);

// Register per-tenant OIDC schemes. Reads tenant_oidc_config from each tenant DB.
// Tenants without a config keep using the global "oidc" scheme (if Authentication:Oidc:Enabled).
await TenantOidcSchemeRegistrar.RegisterAllAsync(app.Services);

app.Services.UseScheduler(scheduler =>
{
    scheduler.Schedule<EmailDispatchJob>()
        .EveryThirtySeconds()
        .PreventOverlapping(nameof(EmailDispatchJob));

    scheduler.Schedule<AuditRetentionJob>()
        .DailyAtHour(3)
        .PreventOverlapping(nameof(AuditRetentionJob));
});

app.Run();

public partial class Program;
