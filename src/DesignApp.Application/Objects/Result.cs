using System.Diagnostics.CodeAnalysis;

namespace DesignApp.Application.Objects;

/// <summary>
/// The outcome of an operation that can be refused for a reason the user should see, such as a bad
/// upload. Expected refusals are values rather than exceptions: they are part of normal traffic, and
/// an exception would also stop the debugger on every bad request.
/// </summary>
/// <typeparam name="T">The value a successful operation returns.</typeparam>
public sealed class Result<T>
{
    private readonly T? _value;

    /// <summary>Use <see cref="Result.Success{T}"/> or <see cref="Result.Failure{T}"/> instead.</summary>
    /// <param name="value">The value on success.</param>
    /// <param name="error">The refusal reason on failure; <c>null</c> on success.</param>
    internal Result(T? value, string? error)
    {
        (_value, Error) = (value, error);
    }

    /// <summary>Why the operation was refused, in words safe to show the user; <c>null</c> on success.</summary>
    public string? Error { get; }

    /// <summary>Whether the operation succeeded. When <c>false</c>, <see cref="Error"/> says why.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    /// <summary>The value of a successful operation.</summary>
    /// <exception cref="InvalidOperationException">The operation failed; check <see cref="Succeeded"/> first.</exception>
    public T Value => Succeeded ? _value! : throw new InvalidOperationException($"No value: {Error}");

    /// <summary>Lets a method return a plain value as a success.</summary>
    /// <param name="value">The successful value.</param>
    public static implicit operator Result<T>(T value)
    {
        return Result.Success(value);
    }
}

/// <summary>Creates <see cref="Result{T}"/> values.</summary>
public static class Result
{
    /// <summary>A successful result.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value to return.</param>
    public static Result<T> Success<T>(T value)
    {
        return new(value, null);
    }

    /// <summary>A refused result.</summary>
    /// <typeparam name="T">The value the operation would have returned.</typeparam>
    /// <param name="error">Why it was refused, in words safe to show the user.</param>
    public static Result<T> Failure<T>(string error)
    {
        return new(default, error);
    }
}
