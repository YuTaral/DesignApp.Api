using System.Diagnostics.CodeAnalysis;

namespace DesignApp.Application.Common;

/// <summary>
/// The outcome of an operation that can be refused for a reason the user should see, such as a bad
/// upload. Expected refusals are values rather than exceptions: they are part of normal traffic, and
/// an exception would also stop the debugger on every bad request.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    internal Result(T? value, string? error) => (_value, Error) = (value, error);

    /// <summary>Why the operation was refused, in words safe to show the user; <c>null</c> on success.</summary>
    public string? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    public T Value => Succeeded ? _value! : throw new InvalidOperationException($"No value: {Error}");

    public static implicit operator Result<T>(T value) => Result.Success(value);
}

public static class Result
{
    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(string error) => new(default, error);
}
