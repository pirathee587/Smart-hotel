using System.Text.RegularExpressions;

namespace SmartHotel.Identity.Application.Common.Security;

public record PasswordPolicyResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static PasswordPolicyResult Success() => new(true, Array.Empty<string>());
    public static PasswordPolicyResult Failure(params string[] errors) => new(false, errors);
    public static PasswordPolicyResult Failure(IEnumerable<string> errors) => new(false, errors.ToList());
}

public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 64;

    public static PasswordPolicyResult Validate(string? password, string? firstName = null, string? lastName = null, string? email = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
            return PasswordPolicyResult.Failure(errors);
        }

        if (password.Length < MinLength)
        {
            errors.Add($"Password must be at least {MinLength} characters long.");
        }

        if (password.Length > MaxLength)
        {
            errors.Add($"Password cannot exceed {MaxLength} characters.");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one uppercase letter.");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add("Password must contain at least one lowercase letter.");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one numeric digit.");
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            errors.Add("Password must contain at least one special character.");
        }

        if (CommonPasswords.Contains(password))
        {
            errors.Add("Password is a commonly used insecure password. Please choose a more unique password.");
        }

        // Check if password contains first name
        if (!string.IsNullOrWhiteSpace(firstName) && firstName.Trim().Length >= 3)
        {
            if (password.Contains(firstName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Password cannot contain your first name.");
            }
        }

        // Check if password contains last name
        if (!string.IsNullOrWhiteSpace(lastName) && lastName.Trim().Length >= 3)
        {
            if (password.Contains(lastName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Password cannot contain your last name.");
            }
        }

        // Check if password contains email local part
        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailTrimmed = email.Trim();
            var atIndex = emailTrimmed.IndexOf('@');
            var localPart = atIndex > 0 ? emailTrimmed[..atIndex] : emailTrimmed;
            if (localPart.Length >= 3 && password.Contains(localPart, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Password cannot contain your email username.");
            }
        }

        return errors.Count == 0 ? PasswordPolicyResult.Success() : PasswordPolicyResult.Failure(errors);
    }

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "12345678", "123456789", "1234567890", "1234567", "password", "password1", "password12", "password123",
        "Password123!", "Password1!", "Password@123", "P@ssword123", "P@ssw0rd123", "P@ssw0rd!", "admin123",
        "Admin123!", "Admin@123", "administrator", "welcome1", "Welcome1!", "Welcome@123", "hotel1234!",
        "SmartHotel1!", "SmartHotel123!", "qwerty1234", "Qwerty1234!", "Qwerty@123", "iloveyou1!", "sunshine1!",
        "princess1!", "dragon123!", "monkey123!", "football1!", "baseball1!", "superman1!", "batman123!",
        "trustno1!", "master123!", "shadow123!", "killer123!", "charlie1!", "michael1!", "jessica1!",
        "summer2024!", "summer2025!", "summer2026!", "winter2024!", "winter2025!", "winter2026!",
        "spring2024!", "spring2025!", "spring2026!", "autumn2024!", "autumn2025!", "autumn2026!",
        "changeme1!", "default123!", "starwars1!", "liverpool1!", "chelsea123!", "arsenal123!",
        "barcelona1!", "realmadrid1!", "pass12345!", "test12345!", "testing123!", "guest1234!",
        "root12345!", "system123!", "operator1!", "manager123!", "reception1!", "housekeeping1!",
        "maintenance1!", "secret123!", "freedom123!", "whatever1!", "coffee123!", "computer1!",
        "internet1!", "security1!", "access123!", "hoteladmin1!", "service123!", "service1!",
        "booking123!", "payment123!", "customer1!", "employee1!", "support123!", "helpdesk1!",
        "login1234!", "portal1234!", "welcome2026!", "hotel2026!", "smarthotel!", "smart2026!",
        "12345678aA!", "Aa123456!", "Password12", "Password123", "Password1234", "Password12345",
        "Abcd1234!", "Abc12345!", "Qwert123!", "Zxcv1234!", "Asdf1234!", "1qaz2wsx3edc!",
        "1qaz@WSX", "qazwsxedc1!", "wsxedcrfv1!", "edcrfvtgb1!", "rfvtgbyhn1!", "tgbnhyujm1!",
        "yhnmjuik1!", "ujmikol1!", "zaq12wsx!", "xsw23edc!", "cde34rfv!", "vfr45tgb!",
        "bgt56yhn!", "nhy67ujm!", "mju78ik!", "looloo12!", "hunter2!A", "hunter21!",
        "letmein12!", "letmein1!", "open12345!", "sesame123!", "opensesame1!", "hello1234!",
        "goodmorning1!", "goodnight1!", "november1!", "december1!", "january12!", "february1!",
        "march1234!", "april1234!", "may123456!", "june12345!", "july12345!", "august123!",
        "september1!", "october12!", "monday123!", "tuesday12!", "wednesday1!", "thursday1!",
        "friday123!", "saturday1!", "sunday123!", "happyday1!", "newyork12!", "london123!",
        "paris1234!", "tokyo1234!", "berlin123!", "sydney123!", "toronto12!", "chicago12!",
        "dallas123!", "miami1234!", "atlanta12!", "seattle12!", "denver123!", "boston123!",
        "alaska123!", "hawaii123!", "canada123!", "america12!", "mexico123!", "brazil123!",
        "spain1234!", "france123!", "italy1234!", "germany12!", "japan1234!", "china1234!",
        "india1234!", "russia123!", "australia1!", "africa123!", "europe123!", "asia12345!",
        "ocean1234!", "mountain1!", "river1234!", "forest123!", "desert123!", "island123!",
        "valley123!", "sunset123!", "sunrise12!", "starlight1!", "moonlight1!", "galaxy123!",
        "universe1!", "cosmos123!", "planet123!", "earth1234!", "jupiter12!", "saturn123!"
    };
}
