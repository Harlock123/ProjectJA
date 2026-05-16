// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Amazon.Runtime;
using Amazon.S3;
using ProjectJA.Infrastructure.Email;
using ProjectJA.Infrastructure.Identity;
using ProjectJA.Infrastructure.Messaging;
using ProjectJA.Infrastructure.Storage;
using ProjectJA.SharedKernel.Storage;
using ProjectJA.Infrastructure.Persistence;
using ProjectJA.Infrastructure.Tenancy;
using ProjectJA.Modules.Identity.Domain;
using ProjectJA.SharedKernel.Messaging;
using ProjectJA.SharedKernel.Tenancy;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextWriter>(sp => sp.GetRequiredService<TenantContext>());

        var defaultConnectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Database=projectja;Username=postgres;Password=postgres";
        var directoryConnectionString = configuration.GetConnectionString("TenantDirectory")
            ?? defaultConnectionString;

        services.AddDbContext<TenantDirectoryDbContext>(opts => opts.UseNpgsql(directoryConnectionString));
        services.AddMemoryCache();
        services.AddScoped<TenantDirectory>();
        var directoryCacheTtl = TimeSpan.FromMinutes(
            configuration.GetValue("Tenancy:DirectoryCacheTtlMinutes", 5));
        services.AddScoped<ITenantDirectory>(sp => new CachedTenantDirectory(
            sp.GetRequiredService<TenantDirectory>(),
            sp.GetRequiredService<IMemoryCache>(),
            directoryCacheTtl));

        // AppDbContext is constructed per scope from the tenant's connection string.
        // ITenantContext must be populated before AppDbContext is resolved — middleware
        // does this for HTTP requests; CircuitHandler does it for Blazor circuits;
        // TenantScopeExtensions.UseTenant does it for out-of-request scopes.
        services.AddScoped<AppDbContext>(sp =>
        {
            var tenant = sp.GetRequiredService<ITenantContext>();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(tenant.ConnectionString)
                .Options;
            return new AppDbContext(options);
        });

        // Modules consume the base DbContext type to avoid project-reference cycles.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Stamp the tenant_id claim into the auth cookie at sign-in time.
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, TenantStampedClaimsFactory>();

        // Populate ITenantContext from the tenant_id claim when a Blazor Server circuit opens.
        services.AddScoped<CircuitHandler, TenantCircuitHandler>();

        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        var storageEndpoint = configuration["Storage:Endpoint"];
        var storageBucket = configuration["Storage:Bucket"];
        if (!string.IsNullOrWhiteSpace(storageEndpoint) && !string.IsNullOrWhiteSpace(storageBucket))
        {
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
                var credentials = new BasicAWSCredentials(
                    opts.AccessKey ?? string.Empty, opts.SecretKey ?? string.Empty);
                var config = new AmazonS3Config
                {
                    ServiceURL = opts.Endpoint,
                    ForcePathStyle = opts.ForcePathStyle,
                    AuthenticationRegion = opts.Region,
                };
                return new AmazonS3Client(credentials, config);
            });
            services.AddSingleton<IObjectStore, S3ObjectStore>();
        }
        else
        {
            services.AddSingleton<IObjectStore, NullObjectStore>();
        }

        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        var emailDriver = configuration["Email:Driver"]?.ToLowerInvariant();
        switch (emailDriver)
        {
            case "smtp":
                services.AddScoped<SharedKernel.Email.IEmailDispatcher, SmtpEmailDispatcher>();
                break;
            case "postmark":
                services.AddHttpClient<PostmarkEmailDispatcher>();
                services.AddScoped<SharedKernel.Email.IEmailDispatcher>(sp =>
                    sp.GetRequiredService<PostmarkEmailDispatcher>());
                break;
            default:
                services.AddScoped<SharedKernel.Email.IEmailDispatcher, LoggingEmailDispatcher>();
                break;
        }

        // Callers always go through the durable outbox queue. The EmailDispatchJob
        // (registered + scheduled in Host/Program.cs) drains the queue using the
        // IEmailDispatcher above.
        services.AddScoped<SharedKernel.Email.IEmailSender, QueuedEmailSender>();
        services.AddScoped<IEmailOutboxAdmin, EmailOutboxAdmin>();
        services.AddTransient<EmailDispatchJob>();

        return services;
    }
}
