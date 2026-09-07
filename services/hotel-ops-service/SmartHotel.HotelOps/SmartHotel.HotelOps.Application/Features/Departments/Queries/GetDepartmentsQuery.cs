using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.HotelOps.Application.Common;
using SmartHotel.HotelOps.Application.Features.Departments.DTOs;
using SmartHotel.HotelOps.Application.Interfaces;
using SmartHotel.HotelOps.Domain.Entities;

namespace SmartHotel.HotelOps.Application.Features.Departments.Queries;

public record GetDepartmentsQuery(Guid? HotelId = null) : IRequest<Result<List<DepartmentDto>>>;

public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, Result<List<DepartmentDto>>>
{
    private readonly IHotelOpsDbContext _context;

    public GetDepartmentsQueryHandler(IHotelOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DepartmentDto>>> Handle(GetDepartmentsQuery request, CancellationToken ct)
    {
        var query = _context.Departments.AsNoTracking();

        if (request.HotelId.HasValue)
        {
            query = query.Where(d => d.HotelId == request.HotelId.Value);
        }

        var list = await query.OrderBy(d => d.Name).ToListAsync(ct);

        var dtos = list.Select(d => new DepartmentDto
        {
            Id = d.Id,
            HotelId = d.HotelId,
            Name = d.Name,
            Description = d.Description,
            ManagerId = d.ManagerId
        }).ToList();

        return Result<List<DepartmentDto>>.Success(dtos);
    }
}
