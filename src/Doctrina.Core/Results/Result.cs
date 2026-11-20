using System.Diagnostics.CodeAnalysis;

namespace Doctrina.Core.Results;

/// <summary>
/// Outcome of an operation without a value: success, or an expected <see cref="Results.Error"/>.
/// Unexpected failures remain exceptions (proposal §3.6).
/// </summary>
public class Result
{
    /// <summary>Initializes an outcome; exactly one of success or error.</summary>
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess == (error is not null))
        {
            throw new ArgumentException("Een succesvolle uitkomst heeft geen fout; een mislukte uitkomst heeft er precies één.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed.</summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    /// <summary>The error; only set if <see cref="IsFailure"/>.</summary>
    public Error? Error { get; }

    /// <summary>A successful outcome.</summary>
    public static Result Success() => new(true, null);

    /// <summary>A failed outcome with <paramref name="error"/>.</summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, error);
    }

    /// <summary>Turns an <see cref="Results.Error"/> into a failed outcome.</summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Translates the outcome into a single value.</summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess() : onFailure(Error);
    }
}

/// <summary>Outcome of an operation with a value of type <typeparamref name="T"/> on success.</summary>
public sealed class Result<T> : Result
{
    private Result(T value)
        : base(true, null) => Value = value;

    private Result(Error error)
        : base(false, error)
    {
    }

    /// <summary>The value; only meaningful if <see cref="Result.IsSuccess"/>.</summary>
    public T? Value { get; }

    /// <summary>A successful outcome with <paramref name="value"/>.</summary>
#pragma warning disable CA1000 // A factory method on the generic type is deliberate (proposal §3.6).
    public static Result<T> Success(T value) => new(value);

    /// <summary>A failed outcome with <paramref name="error"/>.</summary>
    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }
#pragma warning restore CA1000

    /// <summary>Turns a value into a successful outcome.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Turns an <see cref="Results.Error"/> into a failed outcome.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>Translates the outcome into a single value.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(Value!) : onFailure(Error);
    }
}
