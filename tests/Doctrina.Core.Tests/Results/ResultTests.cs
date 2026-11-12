using Doctrina.Core.Results;

namespace Doctrina.Core.Tests.Results;

// Proposal §3.6 (Result-pattern); taak 2.1 van add-modular-foundation.
public sealed class ResultTests
{
    private static readonly Error NotFound = Error.NotFound("Sample.Item.NotFound", "Item bestaat niet.");

    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_CarriesTheError()
    {
        var result = Result.Failure(NotFound);

        Assert.True(result.IsFailure);
        Assert.Same(NotFound, result.Error);
    }

    [Fact]
    public void Failure_WithoutError_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));

    [Fact]
    public void Error_ConvertsImplicitlyToFailedResult()
    {
        Result result = NotFound;

        Assert.True(result.IsFailure);
        Assert.Same(NotFound, result.Error);
    }

    [Fact]
    public void Match_OnResult_PicksTheBranch()
    {
        Assert.Equal("ok", Result.Success().Match(() => "ok", e => e.Code));
        Assert.Equal(NotFound.Code, Result.Failure(NotFound).Match(() => "ok", e => e.Code));
    }

    [Fact]
    public void GenericSuccess_CarriesTheValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericFailure_HasNoValue()
    {
        var result = Result<string>.Failure(NotFound);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Same(NotFound, result.Error);
    }

    [Fact]
    public void Value_ConvertsImplicitlyToSuccessfulResult()
    {
        Result<string> result = "Doctrina";

        Assert.True(result.IsSuccess);
        Assert.Equal("Doctrina", result.Value);
    }

    [Fact]
    public void Error_ConvertsImplicitlyToFailedGenericResult()
    {
        Result<string> result = NotFound;

        Assert.True(result.IsFailure);
        Assert.Same(NotFound, result.Error);
    }

    [Fact]
    public void GenericResult_IsAResult()
    {
        Result result = Result<int>.Success(1);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Match_OnGenericResult_PicksTheBranch()
    {
        Assert.Equal(43, Result<int>.Success(42).Match(v => v + 1, _ => -1));
        Assert.Equal(-1, Result<int>.Failure(NotFound).Match(v => v + 1, _ => -1));
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Failure)]
    public void ErrorFactories_SetTheType(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Validation => Error.Validation("X.Y", "m"),
            ErrorType.NotFound => Error.NotFound("X.Y", "m"),
            ErrorType.Conflict => Error.Conflict("X.Y", "m"),
            ErrorType.Forbidden => Error.Forbidden("X.Y", "m"),
            ErrorType.Unauthorized => Error.Unauthorized("X.Y", "m"),
            _ => Error.Failure("X.Y", "m"),
        };

        Assert.Equal(type, error.Type);
        Assert.Equal("X.Y", error.Code);
    }

    [Fact]
    public void ValidationError_KeepsFieldErrors()
    {
        var details = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["name"] = ["Verplicht."] };

        var error = Error.Validation("Sample.Item.Invalid", "Ongeldig.", details);

        Assert.Equal(["Verplicht."], error.Details!["name"]);
    }
}
