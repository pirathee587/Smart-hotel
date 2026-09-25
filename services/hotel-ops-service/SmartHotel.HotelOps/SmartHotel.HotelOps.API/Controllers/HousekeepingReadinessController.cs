using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using SmartHotel.Authorization; using SmartHotel.HotelOps.API.Services; using System.Security.Claims;
namespace SmartHotel.HotelOps.API.Controllers;
[ApiController,Route("api/v1/rooms/{roomId:guid}/housekeeping"),Authorize(Policy=HotelPolicies.HousekeepingRoomOperations)]
public class HousekeepingReadinessController(HousekeepingReadinessService service):ControllerBase {
 [HttpPost("start")] public async Task<ActionResult<RoomReadinessResponse>> Start(Guid roomId,[FromBody] CleaningStartedRequest request,CancellationToken ct){if(UserId()!=request.HousekeeperId||DepartmentId()!=request.DepartmentId)return Forbid();try{return Ok(await service.StartAsync(roomId,request,ct));}catch(KeyNotFoundException){return NotFound();}catch(InvalidOperationException ex){return Conflict(new{message=ex.Message});}}
 [HttpPost("inspection"),Authorize(Policy=HotelPolicies.HousekeepingInspection)] public async Task<ActionResult<RoomReadinessResponse>> Inspect(Guid roomId,[FromBody] InspectionDecisionRequest request,CancellationToken ct){var isOwner=User.IsInRole("Owner")||User.FindFirstValue("role")=="Owner";if(UserId()!=request.ManagerId||(!isOwner&&DepartmentId()!=request.DepartmentId))return Forbid();try{return Ok(await service.InspectAsync(roomId,request,ct));}catch(KeyNotFoundException){return NotFound();}catch(InvalidOperationException ex){return Conflict(new{message=ex.Message});}}
 private Guid? UserId()=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var id)?id:null;
 private Guid? DepartmentId()=>Guid.TryParse(User.FindFirstValue("departmentId"),out var id)?id:null;
}
