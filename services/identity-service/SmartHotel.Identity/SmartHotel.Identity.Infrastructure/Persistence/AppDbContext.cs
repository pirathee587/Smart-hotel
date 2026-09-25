using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<GuestAccessAuditLog> GuestAccessAuditLogs => Set<GuestAccessAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("identity");

        // Table-Per-Type (TPT) Mapping
        modelBuilder.Entity<Person>().UseTptMappingStrategy();

        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("Persons");
            entity.HasKey(p => p.Id);

            // Globally unique email across ALL persons
            entity.HasIndex(p => p.Email).IsUnique();

            entity.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(p => p.LastName).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Email).HasMaxLength(255).IsRequired();
            entity.Property(p => p.ContactEmail).HasMaxLength(255);
            entity.Property(p => p.PasswordHash).IsRequired();
            entity.Property(p => p.PreferredLanguage).HasMaxLength(10).HasDefaultValue("en");
            entity.Property(p => p.EmailVerificationToken).HasMaxLength(128);
            entity.Property(p => p.PasswordResetToken).HasMaxLength(128);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.Property(c => c.NationalId).HasMaxLength(50);
            entity.Property(c => c.Nationality).HasMaxLength(50);
            entity.Property(c => c.MagicLinkToken).HasMaxLength(128);
            entity.Property(c => c.Role)
                  .HasConversion<string>()
                  .HasMaxLength(50)
                  .HasDefaultValue(CustomerRole.Guest)
                  .IsRequired();
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.Designation).HasMaxLength(100);
            entity.Property(e => e.DepartmentRoleSlot).HasMaxLength(100);
            entity.HasIndex(e => e.DepartmentRoleSlot).IsUnique();

            // Approval workflow columns
            entity.Property(e => e.Status)
                  .HasConversion<string>()
                  .HasMaxLength(30)
                  .HasDefaultValue(EmployeeStatus.Active)
                  .IsRequired();

            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.NationalId).HasMaxLength(50);
            entity.Property(e => e.NicPhotoUrl).HasMaxLength(1000);
            entity.Property(e => e.RequiresProfileCompletion).HasDefaultValue(false);
            entity.Property(e => e.ProfilePhotoUrl).HasMaxLength(1000);
            entity.Property(e => e.BankName).HasMaxLength(150);
            entity.Property(e => e.BankAccountName).HasMaxLength(200);
            entity.Property(e => e.BankAccountNumber).HasMaxLength(100);
            entity.Property(e => e.BankBranch).HasMaxLength(150);
            entity.Property(e => e.SuspensionReason).HasMaxLength(500);

            entity.HasOne(e => e.Department)
                  .WithMany(d => d.Employees)
                  .HasForeignKey(e => e.DepartmentId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).HasMaxLength(100).IsRequired();
            entity.Property(d => d.Description).HasMaxLength(500);
            entity.HasIndex(d => d.ManagerId).IsUnique();

            entity.HasOne(d => d.Manager)
                  .WithMany()
                  .HasForeignKey(d => d.ManagerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.ToTable("ApprovalRequests");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Type).HasMaxLength(100).IsRequired();
            entity.Property(a => a.Description).HasMaxLength(1000).IsRequired();
            entity.Property(a => a.RequestedBy).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Status).HasMaxLength(30).IsRequired();
            entity.HasIndex(a => a.Status);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
            entity.Property(t => t.RevokedAtUtc).IsConcurrencyToken();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.UserId);
            entity.HasOne(t => t.User)
                  .WithMany()
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GuestAccessAuditLog>(entity =>
        {
            entity.ToTable("GuestAccessAuditLogs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.ActorRole).HasMaxLength(50);
            entity.Property(a => a.DepartmentCode).HasMaxLength(50);
            entity.Property(a => a.Action).HasMaxLength(50);
            entity.Property(a => a.SearchQuery).HasMaxLength(255);
            entity.HasIndex(a => a.TimestampUtc);
            entity.HasIndex(a => a.ActorUserId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Employee>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var employee = entry.Entity;
            employee.DepartmentRoleSlot = employee.IsActive && employee.Status == EmployeeStatus.Active &&
                                          employee.Role is EmployeeRole.Admin or EmployeeRole.Manager
                ? $"{employee.Role}:{employee.DepartmentId:D}"
                : null;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
