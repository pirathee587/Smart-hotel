namespace SmartHotel.Booking.Application.Interfaces;

/// <summary>
/// Classifies the conclusive outcome of a payment attempt.
/// </summary>
public enum PaymentExecutionOutcome
{
    /// <summary>
    /// The payment provider confirmed the charge was collected.
    /// </summary>
    Success,

    /// <summary>
    /// The payment provider returned an explicit, conclusive decline
    /// (e.g. insufficient funds, invalid card). The reservation MUST be
    /// released and the draft reset so the guest may retry with a new card.
    /// </summary>
    ConfirmedDecline,

    /// <summary>
    /// The outcome could not be determined (timeout, network error, HTTP 5xx,
    /// missing/ambiguous response). The reservation MUST NOT be released
    /// automatically; manual reconciliation is required.
    /// </summary>
    Unknown
}

/// <summary>
/// Result returned by <see cref="IPaymentGateway.ProcessTokenizedPaymentAsync"/>.
/// </summary>
/// <param name="Outcome">Classified outcome — never infer decline from non-Success alone.</param>
/// <param name="TransactionId">Provider transaction ID; empty when outcome is not Success.</param>
/// <param name="ErrorMessage">Human-readable message for logging or user display.</param>
public record PaymentProcessResult(
    PaymentExecutionOutcome Outcome,
    string TransactionId,
    string ErrorMessage = "");

public interface IPaymentGateway
{
    // TODO: integrate real payment provider SDK
    Task<PaymentProcessResult> ProcessTokenizedPaymentAsync(
        string paymentToken,
        decimal amount,
        string currency,
        string bookingReference,
        CancellationToken ct = default);
}
