using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SmartHotel.Booking.API.Services;

public record VerifiedAttendanceSummary(Guid Id, Guid EmployeeId, Guid DepartmentId, int PayrollYear, int PayrollMonth, int SummaryVersion, string VerificationStatus, int MissingPunches, int AttendanceDisputes, decimal ApprovedOvertimeHours);
public interface IAttendanceSummaryVerifier { Task<bool> IsCurrentVerifiedAsync(Guid id, Guid employeeId, Guid departmentId, int year, int month, int version, decimal overtimeHours, string? authorization, CancellationToken ct); }
public sealed class AttendanceSummaryVerifier(HttpClient client) : IAttendanceSummaryVerifier
{
    public async Task<bool> IsCurrentVerifiedAsync(Guid id, Guid employeeId, Guid departmentId, int year, int month, int version, decimal overtimeHours, string? authorization, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/attendance-summaries/{id}/verified");
        if (AuthenticationHeaderValue.TryParse(authorization, out var header)) request.Headers.Authorization = header;
        using var response = await client.SendAsync(request, ct); if (!response.IsSuccessStatusCode) return false;
        var summary = await response.Content.ReadFromJsonAsync<VerifiedAttendanceSummary>(cancellationToken: ct);
        return summary is not null && summary.Id == id && summary.EmployeeId == employeeId && summary.DepartmentId == departmentId && summary.PayrollYear == year && summary.PayrollMonth == month && summary.SummaryVersion == version && summary.VerificationStatus == "Verified" && summary.MissingPunches == 0 && summary.AttendanceDisputes == 0 && summary.ApprovedOvertimeHours == overtimeHours;
    }
}
