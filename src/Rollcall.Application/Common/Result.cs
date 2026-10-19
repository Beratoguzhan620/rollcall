namespace Rollcall.Application.Common;

public enum ResultStatus
{
    Ok,
    Invalid,
    NotFound,
}

/// <summary>Outcome of a service call: success, a message for the user (optionally tied to a form field), or not found.</summary>
public class Result
{
    public ResultStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>Name of the input property the message belongs to, when there is one.</summary>
    public string? Field { get; init; }

    public bool Success => Status == ResultStatus.Ok;

    public static Result Ok(string message = "") => new() { Status = ResultStatus.Ok, Message = message };

    public static Result Invalid(string message, string? field = null) => new() { Status = ResultStatus.Invalid, Message = message, Field = field };

    public static Result NotFound(string message = "Kayıt bulunamadı.") => new() { Status = ResultStatus.NotFound, Message = message };
}

public sealed class Result<T> : Result
{
    public T? Value { get; init; }

    public static Result<T> Ok(T value, string message = "") => new() { Status = ResultStatus.Ok, Value = value, Message = message };

    public static new Result<T> Invalid(string message, string? field = null) => new() { Status = ResultStatus.Invalid, Message = message, Field = field };

    public static new Result<T> NotFound(string message = "Kayıt bulunamadı.") => new() { Status = ResultStatus.NotFound, Message = message };
}

public sealed class PagedList<T>
{
    public List<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
