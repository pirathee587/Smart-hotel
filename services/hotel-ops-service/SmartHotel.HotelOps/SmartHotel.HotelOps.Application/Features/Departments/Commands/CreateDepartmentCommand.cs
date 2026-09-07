using MediatR;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.Departments.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Domain.Entities;

namespace SmartHotel.HotelOps.Application.Features.Departments.Commands;

public record CreateDepartmentCommand(CreateDepartmentRequest Request) : IRequest<Result<DepartmentDto>>;

public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, Result<DepartmentDto>>
{
    private readonly IHotelOpsDbContext _context;
    private readonly IIdentityServiceClient _identityClient;

    public CreateDepartmentCommandHandler(IHotelOpsDbContext context, IIdentityServiceClient identityClient)
    {
        _context = context;
        _identityClient = identityClient;
    }

    public async Task<Result<DepartmentDto>> Handle(CreateDepartmentCommand command, CancellationToken ct)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Result<DepartmentDto>.Failure("Department name is required.");
        }

        if (req.ManagerId.HasValue)
        {
            // Validates manager via gRPC client abstraction (currently passes through as confirmed)
            var managerValid = await _identityClient.ValidateManagerExistsAsync(req.ManagerId.Value, ct);
            if (!managerValid)
            {
                return Result<DepartmentDto>.Failure($"Manager with ID {req.ManagerId.Value} could not be validated.");
            }
        }

        var department = new Department
        {
            HotelId = req.HotelId,
            Name = req.Name.Trim(),
            Description = req.Description.Trim(),
            ManagerId = req.ManagerId
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync(ct);

        var dto = new DepartmentDto
        {
            Id = department.Id,
            HotelId = department.HotelId,
            Name = department.Name,
            Description = department.Description,
            ManagerId = department.ManagerId
        };

        return Result<DepartmentDto>.Success(dto, "Department created successfully.");
    }
}
