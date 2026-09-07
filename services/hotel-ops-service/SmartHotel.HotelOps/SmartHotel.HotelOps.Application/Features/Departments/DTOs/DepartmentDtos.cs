namespace SmartHotel.HotelOps.Application.Features.Departments.DTOs;

public class DepartmentDto
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ManagerId { get; set; }
}

public class CreateDepartmentRequest
{
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ManagerId { get; set; }
}
