namespace SmartHotel.Booking.Application.Common;

public class Result<T>
{
    public bool Succeeded { get; private set; }
    public T? Data { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public List<string> Errors { get; private set; } = new();

    public static Result<T> Success(T data, string message = "") => new()
    {
        Succeeded = true,
        Data = data,
        Message = message
    };

    public static Result<T> Failure(string message, IEnumerable<string>? errors = null) => new()
    {
        Succeeded = false,
        Message = message,
        Errors = errors?.ToList() ?? new List<string> { message }
    };
}
