using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.Domain.Entities;

namespace SmartHotel.Identity.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<Person> Persons { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Department> Departments { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<GuestAccessAuditLog> GuestAccessAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
