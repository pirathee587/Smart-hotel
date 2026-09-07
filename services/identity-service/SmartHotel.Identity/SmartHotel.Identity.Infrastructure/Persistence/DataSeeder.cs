using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.Infrastructure.Persistence;

public static class DataSeeder
{
    public const string AdminEmail = "admin@smarthotel.internal";
    public const string AdminDefaultPassword = "Admin@SmartHotel2026!";

    public static async Task SeedAsync(AppDbContext context, IPasswordHasher passwordHasher, ILogger logger)
    {
        try
        {
            var adminExists = await context.Employees.AnyAsync(e => e.Email.ToLower() == AdminEmail.ToLower());
            if (!adminExists)
            {
                logger.LogInformation("Seeding initial test Admin employee: {Email}", AdminEmail);

                var admin = new Employee
                {
                    FirstName = "System",
                    LastName = "Administrator",
                    Email = AdminEmail,
                    ContactEmail = "admin-notifications@smarthotel.internal",
                    PasswordHash = passwordHasher.HashPassword(AdminDefaultPassword),
                    Role = EmployeeRole.Admin,
                    IsActive = true,
                    EmailVerified = true,
                    MustChangePassword = false,
                    PreferredLanguage = "en"
                };

                context.Employees.Add(admin);
                await context.SaveChangesAsync();

                logger.LogInformation("Seeded initial test Admin employee successfully.");
            }

            // Seed demo accounts matching login quick-fill
            var demoAdminExists = await context.Employees.AnyAsync(e => e.Email.ToLower() == "admin@smarthotel.com");
            if (!demoAdminExists)
            {
                context.Employees.Add(new Employee
                {
                    FirstName = "Admin",
                    LastName = "User",
                    Email = "admin@smarthotel.com",
                    PasswordHash = passwordHasher.HashPassword("Admin@123!"),
                    Role = EmployeeRole.Admin,
                    IsActive = true,
                    EmailVerified = true,
                    MustChangePassword = false
                });
            }

            var demoManagerExists = await context.Employees.AnyAsync(e => e.Email.ToLower() == "manager@smarthotel.com");
            if (!demoManagerExists)
            {
                context.Employees.Add(new Employee
                {
                    FirstName = "Manager",
                    LastName = "User",
                    Email = "manager@smarthotel.com",
                    PasswordHash = passwordHasher.HashPassword("Manager@123!"),
                    Role = EmployeeRole.Manager,
                    IsActive = true,
                    EmailVerified = true,
                    MustChangePassword = false
                });
            }

            var demoStaffExists = await context.Employees.AnyAsync(e => e.Email.ToLower() == "staff@smarthotel.com");
            if (!demoStaffExists)
            {
                context.Employees.Add(new Employee
                {
                    FirstName = "Staff",
                    LastName = "Member",
                    Email = "staff@smarthotel.com",
                    PasswordHash = passwordHasher.HashPassword("Staff@123!"),
                    Role = EmployeeRole.Receptionist,
                    IsActive = true,
                    EmailVerified = true,
                    MustChangePassword = false
                });
            }

            var userGoogleEmail = "jeyakumaranpiratheepan20@gmail.com";
            var userGoogleExists = await context.Employees.AnyAsync(e => e.Email.ToLower() == userGoogleEmail.ToLower());
            if (!userGoogleExists)
            {
                context.Employees.Add(new Employee
                {
                    FirstName = "Piratheepan",
                    LastName = "Jeyakumaran",
                    Email = userGoogleEmail,
                    PasswordHash = passwordHasher.HashPassword("Admin@123!"),
                    Role = EmployeeRole.Admin,
                    IsActive = true,
                    EmailVerified = true,
                    MustChangePassword = false
                });
            }

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding initial employees.");
        }
    }
}
