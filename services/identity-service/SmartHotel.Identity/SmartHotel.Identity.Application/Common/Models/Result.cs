namespace SmartHotel.Identity.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; protected set; }
    public string Message { get; protected set; } = string.Empty;
    public IReadOnlyList<string> Errors { get; protected set; } = Array.Empty<string>();

    public static Result Success(string message = "") => new() { Succeeded = true, Message = message };
    public static Result Failure(string message, IEnumerable<string>? errors = null) => new()
    {
        Succeeded = false,
        Message = message,
        Errors = errors?.ToList() ?? new List<string> { message }
    };
}

public class Result<T> : Result
{
    public T? Data { get; private set; }

    public static Result<T> Success(T data, string message = "") => new()
    {
        Succeeded = true,
        Data = data,
        Message = message
    };

    public static new Result<T> Failure(string message, IEnumerable<string>? errors = null) => new()
    {
        Succeeded = false,
        Message = message,
        Errors = errors?.ToList() ?? new List<string> { message }
    };
}
