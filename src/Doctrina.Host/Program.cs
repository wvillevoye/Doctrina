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

// Secrets from the uncommitted appsettings.Secrets.json, right after appsettings.{environment}.json (design D13).
builder.Configuration.AddDoctrinaSecrets(builder.Environment);

// Module system: the catalog is validated at Build(); only enabled modules register services.
builder.Host.UseDoctrinaModules();
builder.Services.AddKnownModules();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDoctrinaAuthentication(builder.Configuration);
builder.Services.AddDoctrinaAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, SessionRevalidatingAuthenticationStateProvider>();
builder.Services.AddSingleton(TimeProvider.System);
// API conventions (design D9): ProblemDetails with translated titles, unexpected errors, OpenAPI, health checks.
builder.Services.AddDoctrinaApi();
builder.Services.AddDoctrinaRateLimits();
builder.Services.AddDoctrinaEvents();
builder.Services.AddHostedService<ModuleMigrationService>();
// Background jobs (change add-bulk-import): the server starts after the migrations, so the database exists.
builder.Services.AddDoctrinaJobs(builder.Configuration);
builder.Services.AddDoctrinaJobServer();
// Design: white-label settings, checked at start-up (design D4).
builder.Services.AddOptions<ThemeSettings>().BindConfiguration(ThemeSettings.Section).ValidateOnStart();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<ThemeSettings>, ThemeSettingsValidator>();

var app = builder.Build();
var modules = app.Services.GetRequiredService<ModuleCatalog>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// /api → ProblemDetails (UnexpectedExceptionHandler); pages → /error (without details).
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

/// <summary>Entry point; public for <c>WebApplicationFactory</c> in integration tests.</summary>
public partial class Program;
