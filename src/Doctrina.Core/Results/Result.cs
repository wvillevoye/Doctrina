using System.Diagnostics.CodeAnalysis;

namespace Doctrina.Core.Results;

/// <summary>
/// Uitkomst van een bewerking zonder waarde: succes, of een verwachte <see cref="Results.Error"/>.
/// Onverwachte fouten blijven exceptions (proposal §3.6).
/// </summary>
public class Result
{
    /// <summary>Initialiseert een uitkomst; precies één van succes of fout.</summary>
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess == (error is not null))
        {
            throw new ArgumentException("Een succesvolle uitkomst heeft geen fout; een mislukte uitkomst heeft er precies één.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Of de bewerking is geslaagd.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>Of de bewerking is mislukt.</summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    /// <summary>De fout; alleen gezet als <see cref="IsFailure"/>.</summary>
    public Error? Error { get; }

    /// <summary>Een geslaagde uitkomst.</summary>
    public static Result Success() => new(true, null);

    /// <summary>Een mislukte uitkomst met <paramref name="error"/>.</summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, error);
    }

    /// <summary>Maakt van een <see cref="Results.Error"/> een mislukte uitkomst.</summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Vertaalt de uitkomst naar één waarde.</summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess() : onFailure(Error);
    }
}

/// <summary>Uitkomst van een bewerking met een waarde van type <typeparamref name="T"/> bij succes.</summary>
public sealed class Result<T> : Result
{
    private Result(T value)
        : base(true, null) => Value = value;

    private Result(Error error)
        : base(false, error)
    {
    }

    /// <summary>De waarde; alleen betekenisvol als <see cref="Result.IsSuccess"/>.</summary>
    public T? Value { get; }

    /// <summary>Een geslaagde uitkomst met <paramref name="value"/>.</summary>
#pragma warning disable CA1000 // Fabrieksmethode op het generieke type is bewust (proposal §3.6).
    public static Result<T> Success(T value) => new(value);

    /// <summary>Een mislukte uitkomst met <paramref name="error"/>.</summary>
    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }
#pragma warning restore CA1000

    /// <summary>Maakt van een waarde een geslaagde uitkomst.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Maakt van een <see cref="Results.Error"/> een mislukte uitkomst.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>Vertaalt de uitkomst naar één waarde.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(Value!) : onFailure(Error);
    }
}
