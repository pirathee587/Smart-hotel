using SmartHotel.Identity.Application.Common.Models;
using SmartHotel.Identity.Application.Features.Auth.Models;

namespace SmartHotel.Identity.Application.Features.Auth.Services;

public interface IAuthService
{
    Task<Result<CustomerRegistrationResponse>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct = default);
    Task<Result<LoginResponse>> CustomerLoginAsync(CustomerLoginRequest request, CancellationToken ct = default);
    Task<Result<LoginResponse>> EmployeeLoginAsync(EmployeeLoginRequest request, CancellationToken ct = default);
    Task<Result> VerifyEmailAsync(string email, string token, CancellationToken ct = default);
    Task<Result> ResendVerificationEmailAsync(string email, CancellationToken ct = default);
    Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, bool isMustChangePasswordScope, CancellationToken ct = default);
    Task<Result<RequestMagicLinkResponse>> RequestMagicLinkAsync(RequestMagicLinkCommand command, CancellationToken ct = default);
    Task<Result<LoginResponse>> VerifyMagicLinkAsync(VerifyMagicLinkCommand command, CancellationToken ct = default);
    Task<Result<LoginResponse>> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct = default);
}
