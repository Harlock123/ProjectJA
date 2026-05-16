// SPDX-License-Identifier: BUSL-1.1
// ProjectJA tenant provisioning CLI.
//
// Usage:
//   dotnet run --project tools/Provisioning -- create \
//     --slug acme \
//     --name "ACME Corp" \
//     --admin-email admin@acme.example.com \
//     [--admin-password "Hunter2!"]
//
// Env config:
//   ConnectionStrings__TenantDirectory          (directory DB)
//   Provisioning__MaintenanceConnection         (postgres maintenance DB — runs CREATE DATABASE)
//   Provisioning__TenantConnectionTemplate      (template like "Host=...;Database=tenant_{slug};...")

using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ProjectJA.Infrastructure.Persistence;
using ProjectJA.Infrastructure.Tenancy;
using ProjectJA.Modules.Identity.Domain;

if (args.Length < 1 || !string.Equals(args[0], "create", StringComparison.OrdinalIgnoreCase))
{
    PrintHelp();
    return 1;
}

var opts = ParseArgs(args.AsSpan(1));
if (!opts.TryGetValue("slug", out var slug) || string.IsNullOrWhiteSpace(slug))
{
    Console.Error.WriteLine("--slug is required.");
    return 1;
}
if (!opts.TryGetValue("admin-email", out var adminEmail) || string.IsNullOrWhiteSpace(adminEmail))
{
    Console.Error.WriteLine("--admin-email is required.");
    return 1;
}
var name = opts.GetValueOrDefault("name") ?? slug;
var adminPassword = opts.GetValueOrDefault("admin-password");
var generatedPassword = adminPassword is null;
adminPassword ??= GenerateStrongPassword();

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("src/ProjectJA.Host/appsettings.json", optional: true)
    .AddJsonFile("src/ProjectJA.Host/appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var directoryConn = config.GetConnectionString("TenantDirectory")
    ?? config.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Set ConnectionStrings__TenantDirectory or ConnectionStrings__Default.");

var maintenanceConn = config["Provisioning:MaintenanceConnection"]
    ?? RewriteDatabase(directoryConn, "postgres")
    ?? throw new InvalidOperationException(
        "Set Provisioning__MaintenanceConnection (a connection to the 'postgres' system DB).");

var tenantConnTemplate = config["Provisioning:TenantConnectionTemplate"]
    ?? RewriteDatabase(directoryConn, "tenant_{slug}")
    ?? throw new InvalidOperationException(
        "Set Provisioning__TenantConnectionTemplate (e.g. \"Host=...;Database=tenant_{slug};...\").");

var tenantConn = tenantConnTemplate.Replace("{slug}", slug, StringComparison.Ordinal);

Console.WriteLine($"Provisioning tenant '{slug}' ({name})");
Console.WriteLine($"  directory connection : {Redact(directoryConn)}");
Console.WriteLine($"  tenant connection    : {Redact(tenantConn)}");

await using (var conn = new NpgsqlConnection(maintenanceConn))
{
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $"CREATE DATABASE \"tenant_{slug}\"";
    await cmd.ExecuteNonQueryAsync();
}
Console.WriteLine($"  ✓ database tenant_{slug} created");

var appOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(tenantConn).Options;
await using (var db = new AppDbContext(appOptions))
{
    await db.Database.MigrateAsync();
}
Console.WriteLine($"  ✓ migrations applied to tenant DB");

var tenantId = Guid.NewGuid();
var directoryOptions = new DbContextOptionsBuilder<TenantDirectoryDbContext>()
    .UseNpgsql(directoryConn).Options;
await using (var directory = new TenantDirectoryDbContext(directoryOptions))
{
    directory.Tenants.Add(new Tenant
    {
        Id = tenantId,
        Slug = slug,
        Name = name,
        ConnectionString = tenantConn,
        CreatedAt = DateTimeOffset.UtcNow,
    });
    await directory.SaveChangesAsync();
}
Console.WriteLine($"  ✓ tenant registered in directory (id={tenantId})");

await using (var db = new AppDbContext(appOptions))
{
    var org = Organization.Create(slug, name, DateTimeOffset.UtcNow);
    db.Organizations.Add(org);
    await db.SaveChangesAsync();

    var hasher = new PasswordHasher<ApplicationUser>();
    var admin = new ApplicationUser
    {
        UserName = adminEmail,
        NormalizedUserName = adminEmail.ToUpperInvariant(),
        Email = adminEmail,
        NormalizedEmail = adminEmail.ToUpperInvariant(),
        FirstName = "Admin",
        LastName = "User",
        OrganizationId = org.Id,
        CreatedAt = DateTimeOffset.UtcNow,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString("N"),
    };
    admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
    db.Users.Add(admin);
    await db.SaveChangesAsync();
}
Console.WriteLine($"  ✓ organization + admin user seeded ({adminEmail})");

if (generatedPassword)
    Console.WriteLine($"\nGenerated admin password: {adminPassword}\nStore it now — it won't be shown again.");

Console.WriteLine("\nDone.");
return 0;

static Dictionary<string, string> ParseArgs(ReadOnlySpan<string> args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (!a.StartsWith("--")) continue;
        var key = a[2..];
        var value = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
        result[key] = value;
    }
    return result;
}

static void PrintHelp()
{
    Console.WriteLine("Usage: provisioning create --slug <slug> --name <name> --admin-email <email> [--admin-password <pwd>]");
}

static string? RewriteDatabase(string connectionString, string newDatabase)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = newDatabase };
    return builder.ConnectionString;
}

static string Redact(string connectionString)
{
    var b = new NpgsqlConnectionStringBuilder(connectionString);
    if (!string.IsNullOrEmpty(b.Password)) b.Password = "***";
    return b.ConnectionString;
}

static string GenerateStrongPassword()
{
    const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%^&*";
    Span<byte> bytes = stackalloc byte[20];
    RandomNumberGenerator.Fill(bytes);
    Span<char> result = stackalloc char[20];
    for (var i = 0; i < bytes.Length; i++)
        result[i] = chars[bytes[i] % chars.Length];
    return new string(result);
}
