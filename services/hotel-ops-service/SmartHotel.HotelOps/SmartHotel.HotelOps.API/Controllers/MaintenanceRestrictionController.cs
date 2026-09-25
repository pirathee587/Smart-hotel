using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SmartHotel.Authorization;using SmartHotel.HotelOps.API.Services;using System.Security.Claims;
namespace SmartHotel.HotelOps.API.Controllers;
[ApiController,Route("api/v1/rooms/{roomId:guid}/maintenance"),Authorize(Policy=HotelPolicies.MaintenanceRoomOperations)]
public class MaintenanceRestrictionController(MaintenanceRestrictionService service):ControllerBase{
 [HttpPost("restrict")]public async Task<IActionResult> Restrict(Guid roomId,RestrictRoomRequest r,CancellationToken ct){var isOwner=User.IsInRole("Owner")||User.FindFirstValue("role")=="Owner";if(UserId()!=r.ActorId||(!isOwner&&Department()!=r.DepartmentId))return Forbid();try{return Ok(await service.Restrict(roomId,r,ct));}catch(KeyNotFoundException){return NotFound();}catch(InvalidOperationException e){return Conflict(new{message=e.Message});}}
 [HttpPost("clear"),Authorize(Policy=HotelPolicies.MaintenanceVerification)]public async Task<IActionResult> Clear(Guid roomId,ClearRoomRestrictionRequest r,CancellationToken ct){var isOwner=User.IsInRole("Owner")||User.FindFirstValue("role")=="Owner";if(UserId()!=r.ManagerId||(!isOwner&&Department()!=r.DepartmentId))return Forbid();try{return Ok(await service.Clear(roomId,r,ct));}catch(KeyNotFoundException){return NotFound();}catch(InvalidOperationException e){return Conflict(new{message=e.Message});}}
 [HttpGet("restriction")]public async Task<IActionResult> Status(Guid roomId,CancellationToken ct)=>Ok(new{roomId,blocked=await service.IsBlocked(roomId,ct)});
 Guid? UserId()=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var id)?id:null;Guid? Department()=>Guid.TryParse(User.FindFirstValue("departmentId"),out var id)?id:null;
}
