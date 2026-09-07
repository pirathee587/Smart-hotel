using FluentAssertions;
using SmartHotel.Identity.Application.Common.Security;
using Xunit;

namespace SmartHotel.Identity.UnitTests;

public class PasswordPolicyTests
{
    [Fact]
    public void Validate_WhenPasswordTooShort_ReturnsLengthError()
    {
        var result = PasswordPolicy.Validate("Aa1!");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least 8 characters"));
    }

    [Fact]
    public void Validate_WhenPasswordTooLong_ReturnsLengthError()
    {
        var longPassword = new string('A', 30) + new string('a', 30) + "123456789!";
        var result = PasswordPolicy.Validate(longPassword);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot exceed 64 characters"));
    }

    [Fact]
    public void Validate_WhenMissingUppercase_ReturnsUppercaseError()
    {
        var result = PasswordPolicy.Validate("lowercase123!");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Validate_WhenMissingLowercase_ReturnsLowercaseError()
    {
        var result = PasswordPolicy.Validate("UPPERCASE123!");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Validate_WhenMissingDigit_ReturnsDigitError()
    {
        var result = PasswordPolicy.Validate("UpperLowerNoDigit!");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one numeric digit.");
    }

    [Fact]
    public void Validate_WhenMissingSpecialCharacter_ReturnsSpecialCharError()
    {
        var result = PasswordPolicy.Validate("UpperLowerDigit123");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one special character.");
    }

    [Theory]
    [InlineData("password")]
    [InlineData("12345678")]
    [InlineData("Password123!")]
    [InlineData("Admin123!")]
    [InlineData("hotel1234!")]
    [InlineData("summer2024!")]
    public void Validate_WhenCommonPassword_ReturnsBlocklistError(string commonPassword)
    {
        var result = PasswordPolicy.Validate(commonPassword);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("commonly used insecure password"));
    }

    [Fact]
    public void Validate_WhenContainsFirstName_ReturnsNameError()
    {
        var result = PasswordPolicy.Validate("SecretJohn2026!", firstName: "John", lastName: "Doe", email: "johndoe@example.com");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password cannot contain your first name.");
    }

    [Fact]
    public void Validate_WhenContainsLastName_ReturnsNameError()
    {
        var result = PasswordPolicy.Validate("SecretDoe2026!", firstName: "John", lastName: "Doe", email: "johndoe@example.com");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password cannot contain your last name.");
    }

    [Fact]
    public void Validate_WhenContainsEmailLocalPart_ReturnsEmailError()
    {
        var result = PasswordPolicy.Validate("SecretGuest99!", firstName: "Alex", lastName: "Smith", email: "guest99@hotel.com");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password cannot contain your email username.");
    }

    [Fact]
    public void Validate_WhenStrongPasswordProvided_ReturnsSuccess()
    {
        var result = PasswordPolicy.Validate("Xy9#mK$29LqZ!vW", firstName: "John", lastName: "Doe", email: "johndoe@example.com");
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
