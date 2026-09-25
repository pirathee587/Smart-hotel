using SmartHotel.Identity.Domain.Common;

namespace SmartHotel.Identity.Domain.Entities;

public class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public Guid? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
