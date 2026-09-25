using SmartHotel.Identity.Domain.Common;

namespace SmartHotel.Identity.Domain.Entities;

public class ApprovalRequest : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public Guid? RequestedByUserId { get; set; }
    public string Status { get; set; } = "PendingApproval";
}

