using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartHotel.Identity.Application.Interfaces;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Persistence;

namespace SmartHotel.Identity.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly string _dbName = "IntegrationTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "inmemory"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Register in-memory DbContext
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName, _root);
            });

            // Ensure database is created and seeded
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

            db.Database.EnsureCreated();
            DataSeeder.SeedAsync(db, hasher, logger).GetAwaiter().GetResult();

            // Seed an employee with MustChangePassword = true for testing restricted scope
            var mustChangeEmp = new Employee
            {
                Id = Guid.NewGuid(),
                FirstName = "FirstDay",
                LastName = "Staff",
                Email = "firstday.staff@smarthotel.internal",
                PasswordHash = hasher.HashPassword("InitialTempPass123!"),
                Role = EmployeeRole.Receptionist,
                IsActive = true,
                EmailVerified = true,
                MustChangePassword = true,
                DepartmentId = Guid.Parse("22222222-2222-2222-2222-222222222221"),
                PreferredLanguage = "en"
            };
            db.Employees.Add(mustChangeEmp);
            db.SaveChanges();
        });
    }
}
