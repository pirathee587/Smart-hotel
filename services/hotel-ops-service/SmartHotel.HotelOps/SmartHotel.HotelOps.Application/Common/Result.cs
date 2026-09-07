namespace SmartHotel.HotelOps.Application.Common;

public class Result
{
    public bool Succeeded { get; }
    public string Message { get; }
    public List<string> Errors { get; }

    protected Result(bool succeeded, string message, IEnumerable<string>? errors = null)
    {
        Succeeded = succeeded;
        Message = message;
        Errors = errors?.ToList() ?? new List<string>();
    }

    public static Result Success(string message = "Operation completed successfully.") => new(true, message);
    public static Result Failure(string message, IEnumerable<string>? errors = null) => new(false, message, errors);
    public static Result Failure(string message, string error) => new(false, message, new[] { error });
}

public class Result<T> : Result
{
    public T? Data { get; }

    protected Result(bool succeeded, string message, T? data, IEnumerable<string>? errors = null)
        : base(succeeded, message, errors)
    {
        Data = data;
    }

    public static Result<T> Success(T data, string message = "Operation completed successfully.") => new(true, message, data);
    public new static Result<T> Failure(string message, IEnumerable<string>? errors = null) => new(false, message, default, errors);
    public new static Result<T> Failure(string message, string error) => new(false, message, default, new[] { error });
}
