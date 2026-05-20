// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Infrastructure.Email;
using ProjectJA.Modules.Audit.Domain;
using ProjectJA.Modules.Audit.Persistence;
using ProjectJA.Modules.Identity.Domain;
using ProjectJA.Modules.Identity.Persistence;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Issues.Persistence;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.Modules.Projects.Persistence;

namespace ProjectJA.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<TenantOidcConfig> TenantOidcConfigs => Set<TenantOidcConfig>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<EmailOutboxEntry> EmailOutbox => Set<EmailOutboxEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new InviteConfiguration());
        modelBuilder.ApplyConfiguration(new TenantOidcConfigConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new SprintConfiguration());
        modelBuilder.ApplyConfiguration(new IssueConfiguration());
        modelBuilder.ApplyConfiguration(new AttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new EmailOutboxEntryConfiguration());

        // ASP.NET Identity tables: keep the default names but lower-cased for Postgres.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is not null && table.StartsWith("AspNet"))
                entity.SetTableName(table.ToLowerInvariant());
        }
    }
}
