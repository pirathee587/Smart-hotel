using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(50).IsRequired();

            entity.HasOne(e => e.Department)
                  .WithMany(d => d.Employees)
                  .HasForeignKey(e => e.DepartmentId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).HasMaxLength(100).IsRequired();

            entity.HasOne(d => d.Manager)
                  .WithMany()
                  .HasForeignKey(d => d.ManagerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
