using Doctrina.Host;
using Doctrina.Host.Api;
using Doctrina.Host.Authentication;
using Doctrina.Host.Components;
using Doctrina.Host.Theming;
using Doctrina.Infrastructure.Api;
using Doctrina.Infrastructure.Authentication;
using Doctrina.Infrastructure.Authorization;
using Doctrina.Infrastructure.Configuration;
using Doctrina.Infrastructure.Events;
using Doctrina.Infrastructure.Jobs;
using Doctrina.Infrastructure.Modules;
using Doctrina.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Secrets uit het niet-gecommitte appsettings.Secrets.json, direct na appsettings.{omgeving}.json (design D13).
builder.Configuration.AddDoctrinaSecrets(builder.Environment);

// Modulesysteem: de catalogus wordt bij Build() gevalideerd; alleen ingeschakelde modules registreren services.
builder.Host.UseDoctrinaModules();
builder.Services.AddKnownModules();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDoctrinaAuthentication(builder.Configuration);
builder.Services.AddDoctrinaAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, SessionRevalidatingAuthenticationStateProvider>();
builder.Services.AddSingleton(TimeProvider.System);
// API-conventies (design D9): ProblemDetails met vertaalde titels, onverwachte fouten, OpenAPI, health checks.
builder.Services.AddDoctrinaApi();
builder.Services.AddDoctrinaRateLimits();
builder.Services.AddDoctrinaEvents();
builder.Services.AddHostedService<ModuleMigrationService>();
// Background jobs (change add-bulk-import): the server starts after the migrations, so the database exists.
builder.Services.AddDoctrinaJobs(builder.Configuration);
builder.Services.AddDoctrinaJobServer();
// Vormgeving: white-label-instellingen, bij het opstarten gecontroleerd (design D4).
builder.Services.AddOptions<ThemeSettings>().BindConfiguration(ThemeSettings.Section).ValidateOnStart();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<ThemeSettings>, ThemeSettingsValidator>();

var app = builder.Build();
var modules = app.Services.GetRequiredService<ModuleCatalog>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// /api → ProblemDetails (UnexpectedExceptionHandler); pagina's → /error (zonder details).
app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandlingPath = "/error", CreateScopeForErrors = true });
app.UseHttpsRedirection();
app.UseDoctrinaRequestLocalization();
app.UseStatusCodePages();
// Unreachable identity provider → notice page instead of /error (change add-idp-unavailable-notice).
app.UseDoctrinaIdentityProviderUnavailable();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapDoctrinaAuthenticationEndpoints();
app.MapDoctrinaThemeEndpoint();
app.MapDoctrinaHealth();
app.MapDoctrinaJobsDashboard();
app.MapDoctrinaApiDocumentation(app.Configuration, app.Environment);
app.MapApiV1().MapDoctrinaModules();
app.MapOoapiV5().MapDoctrinaOoapiModules();
app.MapDoctrinaBrowserEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies([.. modules.ComponentAssemblies]);

await app.RunAsync();

/// <summary>Toegangspunt; publiek voor <c>WebApplicationFactory</c> in integratietests.</summary>
public partial class Program;
