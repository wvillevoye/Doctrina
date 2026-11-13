using System.Globalization;
using System.Reflection;
using System.Resources;
using Doctrina.Core.Results;
using Doctrina.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Doctrina.Infrastructure.Api;

/// <summary>Een <c>.resx</c>-bron met fouttitels; sleutel = foutcode (bv. <c>Academic.Course.NotFound</c>).</summary>
/// <param name="Resources">De resources (neutrale taal = Nederlands, plus <c>.en</c>).</param>
public sealed record ErrorTitleSource(ResourceManager Resources);

/// <summary>
/// Vertaalde titels voor foutcodes (spec api-conventions "Fouttitels zijn vertaald", design D9): de taal van het verzoek
/// (<c>Accept-Language</c>, nl standaard, en), met de eigen melding van de fout als terugval.
/// </summary>
public sealed class ErrorTitles(IEnumerable<ErrorTitleSource> sources) : IErrorTitles
{
    private readonly ErrorTitleSource[] _sources = [.. sources];
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    /// <summary>De vertaling van <paramref name="code"/> in <paramref name="culture"/>, of <paramref name="fallback"/>.</summary>
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

/// <summary>ProblemDetails-afspraken voor alle fouten van de host.</summary>
public static class DoctrinaProblemDetails
{
    /// <summary>
    /// Vult elke ProblemDetails aan: <c>code</c> voor kale 401/403 (<c>Auth.Unauthorized</c>/<c>Auth.Forbidden</c>) en
    /// een vertaalde <c>title</c> op basis van <c>code</c>. <c>traceId</c> voegt ASP.NET Core zelf toe.
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

/// <summary>Registratie van fouttitels.</summary>
public static class ErrorTitleServiceCollectionExtensions
{
    /// <summary>
    /// Registreert de fouttitels uit <c>&lt;baseName&gt;.resx</c> in <paramref name="assembly"/>, bv.
    /// <c>services.AddErrorTitles("Doctrina.Modules.Academic.Resources.AcademicErrors", typeof(AcademicModule).Assembly)</c>.
    /// </summary>
    public static IServiceCollection AddErrorTitles(this IServiceCollection services, string baseName, Assembly assembly)
    {
        services.TryAddSingleton<ErrorTitles>();
        services.TryAddSingleton<IErrorTitles>(sp => sp.GetRequiredService<ErrorTitles>());
        return services.AddSingleton(new ErrorTitleSource(new ResourceManager(baseName, assembly)));
    }
}
