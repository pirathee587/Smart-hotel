using SmartHotel.Identity.Domain.Enums;

namespace SmartHotel.Identity.Domain.Entities;

public class Customer : Person
{
    public string? NationalId { get; set; }
    public string? Nationality { get; set; }
    public string? MagicLinkToken { get; set; }
    public DateTime? MagicLinkTokenExpiresAtUtc { get; set; }

    /// <summary>
    /// The role assigned to this customer, used to determine permissions.
    /// Defaults to <see cref="CustomerRole.Guest"/> for all new registrations.
    /// </summary>
    public CustomerRole Role { get; set; } = CustomerRole.Guest;
}
