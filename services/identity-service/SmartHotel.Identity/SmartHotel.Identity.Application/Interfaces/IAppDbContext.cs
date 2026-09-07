using Microsoft.EntityFrameworkCore;
using SmartHotel.Identity.Domain.Entities;

namespace SmartHotel.Identity.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<Person> Persons { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Department> Departments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
