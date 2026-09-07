using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.Domain.Entities;

public class Employee : Person
{
    public EmployeeRole Role { get; set; } = EmployeeRole.Receptionist;
    
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    
    public bool MustChangePassword { get; set; } = false;
}
