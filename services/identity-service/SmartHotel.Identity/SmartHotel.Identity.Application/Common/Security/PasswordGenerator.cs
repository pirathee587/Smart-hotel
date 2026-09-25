using System.Security.Cryptography;
using System.Text;

namespace SmartHotel.Identity.Application.Common.Security;

/// <summary>
/// Cryptographically secure password generator for employee onboarding and temporary credentials.
/// Produces passwords that strictly satisfy the SmartHotel PasswordPolicy.
/// </summary>
public static class PasswordGenerator
{
    private const string UpperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // omitted ambiguous 'I', 'O'
    private const string LowerChars = "abcdefghijkmnpqrstuvwxyz"; // omitted ambiguous 'l', 'o'
    private const string DigitChars = "23456789";                 // omitted ambiguous '0', '1'
    private const string SpecialChars = "!@#$%*&-_+=";
    private static readonly string AllChars = UpperChars + LowerChars + DigitChars + SpecialChars;

    /// <summary>
    /// Generates a cryptographically strong temporary password (default 14 characters).
    /// Guaranteed to contain uppercase, lowercase, numbers, and symbols.
    /// </summary>
    public static string Generate(int length = 14)
    {
        if (length < 14)
            length = 14;

        while (true)
        {
            var chars = new List<char>(length);

            // Guarantee diversity
            chars.Add(UpperChars[RandomNumberGenerator.GetInt32(UpperChars.Length)]);
            chars.Add(UpperChars[RandomNumberGenerator.GetInt32(UpperChars.Length)]);
            chars.Add(LowerChars[RandomNumberGenerator.GetInt32(LowerChars.Length)]);
            chars.Add(LowerChars[RandomNumberGenerator.GetInt32(LowerChars.Length)]);
            chars.Add(DigitChars[RandomNumberGenerator.GetInt32(DigitChars.Length)]);
            chars.Add(DigitChars[RandomNumberGenerator.GetInt32(DigitChars.Length)]);
            chars.Add(SpecialChars[RandomNumberGenerator.GetInt32(SpecialChars.Length)]);
            chars.Add(SpecialChars[RandomNumberGenerator.GetInt32(SpecialChars.Length)]);

            // Fill the remainder
            while (chars.Count < length)
            {
                chars.Add(AllChars[RandomNumberGenerator.GetInt32(AllChars.Length)]);
            }

            // Cryptographic Fisher-Yates shuffle
            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            var password = new string(chars.ToArray());

            // Validate against SmartHotel password policy
            if (PasswordPolicy.Validate(password).IsValid)
            {
                return password;
            }
        }
    }
}
