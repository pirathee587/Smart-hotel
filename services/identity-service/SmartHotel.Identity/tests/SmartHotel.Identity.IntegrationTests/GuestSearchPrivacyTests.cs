using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.API.Controllers;
using SmartHotel.Identity.Domain.Entities;
using SmartHotel.Identity.Domain.Enums;
using SmartHotel.Identity.Infrastructure.Persistence;
using SmartHotel.Identity.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Identity.IntegrationTests;

public class GuestSearchPrivacyTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static GuestsController CreateController(AppDbContext context, string role = "Receptionist", string department = "FRONTOFFICE", Guid? userId = null)
    {
        var hasher = new PasswordHasher();
        var controller = new GuestsController(context, hasher);
        var uid = (userId ?? Guid.NewGuid()).ToString();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, uid),
            new Claim(ClaimTypes.Role, role),
            new Claim("departmentCode", department),
            new Claim("departmentId", Guid.NewGuid().ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    [Fact]
    public async Task SearchGuests_MasksSensitiveNationalId_PreservingOnlyLastFourCharacters()
    {
        await using var context = CreateContext();

        var guest1 = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Kamal",
            LastName = "Perera",
            Email = "kamal@example.com",
            ContactEmail = "+94771112233",
            NationalId = "912345678V", // 10 chars -> ******678V
            Nationality = "Sri Lankan",
            PasswordHash = "supersecret_hash"
        };
        var guest2 = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Nimal",
            LastName = "Silva",
            Email = "nimal@example.com",
            NationalId = "200012345678", // 12 chars -> ********5678
            Nationality = "Sri Lankan",
            PasswordHash = "supersecret_hash"
        };
        context.Customers.AddRange(guest1, guest2);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var actionResult = await controller.SearchGuests("Perera", 10, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var results = okResult.Value.Should().BeAssignableTo<List<GuestSearchResultDto>>().Subject;

        results.Should().HaveCount(1);
        var res = results[0];
        res.FullName.Should().Be("Kamal Perera");
        res.MaskedNationalId.Should().Be("******678V");
        res.MaskedNationalId.Should().NotContain("912345");
    }

    [Fact]
    public async Task SearchGuests_DataMinimization_DoesNotExposeSensitiveInternalFields()
    {
        await using var context = CreateContext();

        var guest = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Sunil",
            LastName = "Jayawardena",
            Email = "sunil@example.com",
            ContactEmail = "+94772223344",
            NationalId = "852341234V",
            Nationality = "Sri Lankan",
            PasswordHash = "argon2id$v=19$m=65536,t=3,p=4$verysecret"
        };
        context.Customers.Add(guest);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var actionResult = await controller.SearchGuests(null, 10, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var results = okResult.Value.Should().BeAssignableTo<List<GuestSearchResultDto>>().Subject;

        var dto = results.Should().ContainSingle().Subject;
        // Verify only operational fields exist on DTO
        dto.GetType().GetProperty("PasswordHash").Should().BeNull();
        dto.GetType().GetProperty("SecurityStamp").Should().BeNull();
        dto.GetType().GetProperty("RefreshToken").Should().BeNull();
    }

    [Fact]
    public async Task SearchGuests_CreatesImmutableAuditLog_WithActorAndQueryDetails()
    {
        await using var context = CreateContext();

        var guest = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Anura",
            LastName = "Kumara",
            Email = "anura@example.com",
            NationalId = "701234567V",
            Nationality = "Sri Lankan"
        };
        context.Customers.Add(guest);
        await context.SaveChangesAsync();

        var actorId = Guid.NewGuid();
        var controller = CreateController(context, "Receptionist", "FRONTOFFICE", actorId);

        await controller.SearchGuests("Anura", 20, CancellationToken.None);

        var audit = await context.GuestAccessAuditLogs.FirstOrDefaultAsync();
        audit.Should().NotBeNull();
        audit!.ActorUserId.Should().Be(actorId);
        audit.ActorRole.Should().Be("Receptionist");
        audit.DepartmentCode.Should().Be("FRONTOFFICE");
        audit.SearchQuery.Should().Be("Anura");
        audit.ResultCount.Should().Be(1);
        audit.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
