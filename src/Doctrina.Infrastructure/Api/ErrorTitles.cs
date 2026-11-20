using System.Globalization;
using System.Reflection;
using System.Resources;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Doctrina.Infrastructure.Api;

/// <summary>A <c>.resx</c> source with error titles; key = error code (e.g. <c>Academic.Course.NotFound</c>).</summary>
/// <param name="Resources">The resources (neutral language = Dutch, plus <c>.en</c>).</param>
public sealed record ErrorTitleSource(ResourceManager Resources);

/// <summary>
/// Translated titles for error codes (spec api-conventions "Fouttitels zijn vertaald", design D9): the language of the request
/// (<c>Accept-Language</c>, nl default, en), with the error's own message as fallback.
/// </summary>
public sealed class ErrorTitles(IEnumerable<ErrorTitleSource> sources) : IErrorTitles
{
    private readonly ErrorTitleSource[] _sources = [.. sources];
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    /// <summary>The translation of <paramref name="code"/> in <paramref name="culture"/>, or <paramref name="fallback"/>.</summary>
    public string For(string code, string fallback, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        culture ??= CultureInfo.CurrentUICulture;
        foreach (var source in _sources)
        {
            var title = source.Resources.GetString(code, culture);
            if (!string.IsNullOrEmpty(title))
            {
                return title;
            }
        }

        return fallback;
    }

    /// <inheritdoc />
    /// <remarks>The UI is English (change switch-ui-to-english, design D2); the API keeps following the request culture.</remarks>
    string IErrorTitles.For(string code, string fallback) => For(code, fallback, English);
}

/// <summary>ProblemDetails conventions for all errors of the host.</summary>
public static class DoctrinaProblemDetails
{
    /// <summary>
    /// Completes every ProblemDetails: <c>code</c> for bare 401/403 (<c>Auth.Unauthorized</c>/<c>Auth.Forbidden</c>) and
    /// a translated <c>title</c> based on <c>code</c>. ASP.NET Core adds <c>traceId</c> itself.
    /// </summary>
    public static void Customize(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        AuthorizationProblemDetails.AddErrorCode(context);
        if (context.ProblemDetails.Extensions.TryGetValue("code", out var value) && value is string code)
        {
            var titles = context.HttpContext.RequestServices.GetService<ErrorTitles>();
            if (titles is not null)
            {
                context.ProblemDetails.Title = titles.For(code, context.ProblemDetails.Title ?? code);
            }
        }
    }
}

/// <summary>Registration of error titles.</summary>
public static class ErrorTitleServiceCollectionExtensions
{
    /// <summary>
    /// Registers the error titles from <c>&lt;baseName&gt;.resx</c> in <paramref name="assembly"/>, e.g.
    /// <c>services.AddErrorTitles("Doctrina.Modules.Academic.Resources.AcademicErrors", typeof(AcademicModule).Assembly)</c>.
    /// </summary>
    public static IServiceCollection AddErrorTitles(this IServiceCollection services, string baseName, Assembly assembly)
    {
        services.TryAddSingleton<ErrorTitles>();
        services.TryAddSingleton<IErrorTitles>(sp => sp.GetRequiredService<ErrorTitles>());
        return services.AddSingleton(new ErrorTitleSource(new ResourceManager(baseName, assembly)));
    }
}
