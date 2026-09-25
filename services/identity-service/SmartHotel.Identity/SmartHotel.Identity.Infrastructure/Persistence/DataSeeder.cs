using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.Infrastructure.Persistence;

public static class DataSeeder
{
    private static readonly Guid UnassignedDepartmentId = Guid.Parse("22222222-2222-2222-2222-222222222220");
    private static readonly Guid FrontOfficeDepartmentId = Guid.Parse("22222222-2222-2222-2222-222222222221");
    private static readonly Guid HousekeepingDepartmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MaintenanceDepartmentId = Guid.Parse("22222222-2222-2222-2222-222222222223");
    public const string AdminEmail = "admin@smarthotel.internal";
    public const string AdminDefaultPassword = "Admin@SmartHotel2026!";

    public static async Task SeedAsync(AppDbContext context, IPasswordHasher passwordHasher, ILogger logger)
    {
        try
        {
            var requiredDepartments = new[]
            {
                new Department { Id = UnassignedDepartmentId, Name = "Unassigned — migration review required", Description = "Technical holding department for records awaiting an authorized assignment." },
                new Department { Id = FrontOfficeDepartmentId, Name = "Front Office", Description = "Guest check-in, concierge, and reservations reception." },
                new Department { Id = HousekeepingDepartmentId, Name = "Housekeeping", Description = "Room cleaning, linen inspection, and sanitization." },
                new Department { Id = MaintenanceDepartmentId, Name = "Maintenance", Description = "Facility repairs, HVAC, electrical, and plumbing." },
                new Department { Id = Guid.Parse("22222222-2222-2222-2222-222222222224"), Name = "Food & Beverage", Description = "Kitchen, restaurant, room service, and beverage operations." },
                new Department { Id = Guid.Parse("22222222-2222-2222-2222-222222222225"), Name = "Finance", Description = "Revenue, expenses, refunds, payroll review, reporting, and financial controls." }
            };

            var existingDepartmentIds = await context.Departments
                .Where(d => requiredDepartments.Select(required => required.Id).Contains(d.Id))
                .Select(d => d.Id)
                .ToListAsync();
            context.Departments.AddRange(requiredDepartments.Where(d => !existingDepartmentIds.Contains(d.Id)));
            await context.SaveChangesAsync();

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
                    ,DepartmentId = UnassignedDepartmentId
                };

                context.Employees.Add(admin);
                await context.SaveChangesAsync();

                logger.LogInformation("Seeded initial test Admin employee successfully.");
            }

            // Seed demo accounts matching login quick-fill
            var demoOwnerExists = await context.Employees.AnyAsync(e => e.Email.ToLower() == "owner@smarthotel.com");
            if (!demoOwnerExists)
            {
                context.Employees.Add(new Employee
                {
                    FirstName = "Demo",
                    LastName = "Owner",
                    Email = "owner@smarthotel.com",
                    PasswordHash = passwordHasher.HashPassword("Owner@123!"),
                    Role = EmployeeRole.Owner,
                    IsActive = true,
                    EmailVerified = true,
                    MustChangePassword = false
                    ,DepartmentId = UnassignedDepartmentId
                });
            }

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
                    ,DepartmentId = FrontOfficeDepartmentId
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
                    ,DepartmentId = HousekeepingDepartmentId
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
                    ,DepartmentId = FrontOfficeDepartmentId
                });
            }

            var demoEmployeeExists = await context.Employees.AnyAsync(e => e.Email.ToLower() == "employee@smarthotel.com");
            if (!demoEmployeeExists)
            {
                context.Employees.Add(new Employee
                {
                    FirstName = "Demo",
                    LastName = "Employee",
                    Email = "employee@smarthotel.com",
                    PasswordHash = passwordHasher.HashPassword("Employee@123!"),
                    Role = EmployeeRole.Housekeeper,
                    IsActive = true,
                    EmailVerified = true,
                    MustChangePassword = false
                    ,DepartmentId = HousekeepingDepartmentId
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
                    ,DepartmentId = MaintenanceDepartmentId
                });
            }

            var memberEmail = "member@smarthotel.com";
            var memberExists = await context.Customers.AnyAsync(c => c.Email.ToLower() == memberEmail.ToLower());
            if (!memberExists)
            {
                context.Customers.Add(new Customer
                {
                    FirstName = "Eleanor",
                    LastName = "Vance",
                    Email = memberEmail,
                    PasswordHash = passwordHasher.HashPassword("Member@123!"),
                    IsActive = true,
                    EmailVerified = true,
                    PreferredLanguage = "en"
                });
            }

            await context.SaveChangesAsync();

            // Keep development quick-login accounts useful even on databases seeded by
            // older versions. This is idempotent and does not touch credentials.
            var demoAssignments = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin@smarthotel.com"] = FrontOfficeDepartmentId,
                ["manager@smarthotel.com"] = HousekeepingDepartmentId,
                ["staff@smarthotel.com"] = FrontOfficeDepartmentId,
                ["employee@smarthotel.com"] = HousekeepingDepartmentId,
                ["jeyakumaranpiratheepan20@gmail.com"] = MaintenanceDepartmentId
            };
            var demoEmployees = await context.Employees
                .Where(e => demoAssignments.Keys.Contains(e.Email))
                .ToListAsync();
            foreach (var employee in demoEmployees) employee.DepartmentId = demoAssignments[employee.Email];

            var demoManager = demoEmployees.FirstOrDefault(e => e.Email == "manager@smarthotel.com");
            var housekeeping = await context.Departments.FirstOrDefaultAsync(d => d.Id == HousekeepingDepartmentId);
            if (demoManager is not null && housekeeping is not null) housekeeping.ManagerId = demoManager.Id;
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding initial employees.");
        }
    }
}
