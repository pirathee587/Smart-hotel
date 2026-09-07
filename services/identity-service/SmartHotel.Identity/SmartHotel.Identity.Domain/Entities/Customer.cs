namespace SmartHotel.Identity.Domain.Entities;

public class Customer : Person
{
    public string? NationalId { get; set; }
    public string? Nationality { get; set; }
    public string? MagicLinkToken { get; set; }
    public DateTime? MagicLinkTokenExpiresAtUtc { get; set; }
}
