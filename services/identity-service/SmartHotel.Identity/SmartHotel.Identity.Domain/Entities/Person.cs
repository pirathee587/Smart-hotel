using SmartHotel.Identity.Domain.Common;

namespace SmartHotel.Identity.Domain.Entities;

public abstract class Person : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    /// <summary>
    /// Login identifier (globally unique across ALL persons).
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Real address for notifications — Employee only, distinct from Email
    /// since Employee's Email will later be system-generated;
    /// for Customer, Email IS the real contact address.
    /// </summary>
    public string? ContactEmail { get; set; }
    
    public string PasswordHash { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public bool EmailVerified { get; set; } = false;
    
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiresAtUtc { get; set; }
    
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAtUtc { get; set; }
    
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>
    /// Returns the effective contact email: ContactEmail if populated, otherwise login Email.
    /// </summary>
    public string GetEffectiveNotificationEmail() =>
        !string.IsNullOrWhiteSpace(ContactEmail) ? ContactEmail : Email;
}
