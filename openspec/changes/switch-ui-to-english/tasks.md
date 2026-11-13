# Taken

## 1. Paden

- [x] 1.1 Zet de padconstanten op Engels (design D3): `SignInPath`, `SignOutPath`, `UnavailablePath`, `SignedOutParameter` (`signedOut`), `AccessDeniedPath`, `DcTheme.EndpointPath`, `ExceptionHandlingPath` in `Program.cs` en `ProfileHref` in `DcAccountMenu`/`DcAppShell`; pas de verwachtingen in de bestaande tests aan (bij voorkeur via de constanten); controleer met `dotnet test` dat de bestaande tests voor inloggen, uitloggen, onbereikbare inlogdienst, toegang en thema slagen met de nieuwe paden
- [x] 1.2 Hernoem de paginacomponenten en hun `@page`-routes (design D4): `Profiel` → `Profile` (`/profile`), `GeenToegang` → `AccessDenied` (`/access-denied`), `Fout` → `Error` (`/error`), `Stijlgids` → `StyleGuide` (`/style-guide`), `NietToegestaan` → `NotAuthorized`, met `git mv`; de samplepagina's in `Doctrina.TestKit` naar `/sample/error` en `/sample/manage`; controleer met `dotnet build` (warnings = errors) en de bestaande tests voor profiel, stijlgids en foutpagina
- [x] 1.3 Schrijf test `DeGebruikersinterfaceIsEngels.EngelsePaden` in `tests/Doctrina.Host.IntegrationTests/HostUi`: ingelogd opent `/profile` de profielpagina en geeft `/profiel` 404; controleer dat de test slaagt
- [x] 1.4 Werk de paden bij in `docs/ontwikkelen/starten.md`, `docs/ontwikkelen/modules.md`, `docs/ontwikkelen/vormgeving.md`, `docs/doctrina-integratie.md` en `dev-stack/README.md`; controleer met een zoekactie dat geen van de oude paden uit de tabel in `proposal.md` er nog in staat

## 2. Teksten

- [x] 2.1 Maak de teksten in de `Dc*`-componenten Engels (design D1): skiplink "Skip to content", menuknop "Menu", accountmenu "Sign in"/"My profile"/"Sign out", themakeuze "Light"/"Dark"/"System", "(required)", "(busy)", sluitknop van de dialoog, ernstlabels van `DcAlert`; pas de bUnit-tests in `tests/Doctrina.UI.Tests` aan; controleer dat die slagen
- [x] 2.2 Maak de teksten van de host Engels: `App.razor` (`lang="en"`), layout en hoofdmenu ("Home", "My profile"), start, profiel, geen toegang ("Access denied"), foutpagina en stijlgids, inclusief paginatitels ("<pagina> · Doctrina"); pas de integratietests aan op de scenario's van `app-shell`, `theming`, `user-authentication` en `access-control` uit de spec-delta (o.a. `HostUiTests` voor opbouw, paginatitel "My profile · Doctrina", accountgedeelte en menu; `LichtEnDonkerThemaNaarKeuzeVanDeGebruiker` voor "Dark"/"System"; `AccessControlTests.PaginaGeweigerdZonderPermissie` voor "Access denied"); controleer dat ze slagen
- [x] 2.3 Werk schermteksten bij die de documentatie uit 1.4 noemt (bv. "Inloggen", "Mijn profiel", "Stijlgids"); controleer met een zoekactie in die bestanden

## 3. Fouttitels in de UI

- [x] 3.1 Laat `IErrorTitles.For` in `ErrorTitles` altijd de Engelse titel geven en pas het XML-commentaar van `IErrorTitles` aan (design D2); controleer dat `ApiConventionsTests` (API volgt `Accept-Language`, nl standaard) en `ComponentenKrijgenDezelfdeTitelsViaIErrorTitles` blijven slagen
- [x] 3.2 Voeg in `Doctrina.TestKit` een samplepagina `/sample/result` toe die een fout met een Nederlandse en Engelse titel uit `SampleErrors` via `DcResult` toont, en schrijf test `DeGebruikersinterfaceIsEngels.FouttitelInDeUiIsEngelsBijEenNederlandseBrowser` (verzoek met `Accept-Language: nl`, de melding toont de Engelse titel); controleer dat de test slaagt

## 4. Afronding

- [x] 4.1 Zoek in `src/` en `tests/Doctrina.TestKit` naar de oude paden en naar Nederlandse woorden in `.razor`-bestanden (bv. "Inloggen", "Uitloggen", "profiel", "verplicht", "bezig", "Sluiten", "Naar de inhoud"); controleer dat er niets overblijft behalve codecommentaar en foutmeldingen voor ontwikkelaars (buiten de scope)
- [x] 4.2 Draai `dotnet test` voor de hele solution en `openspec validate switch-ui-to-english --strict`; controleer dat alles slaagt en dat elk scenario uit de spec-delta een test heeft
- [x] 4.3 Handmatig met de dev-stack: inloggen via "Sign in", "My profile" openen, thema "Dark" kiezen, "Sign out"; controleer dat elk scherm Engels is en de paden Engels zijn

## Vervolg in de workflow

- Beoordeel de change vóór `/opsx:apply`.
- Archiveren pas na `add-design-system` en `add-idp-unavailable-notice`.
- Na afronding: commit op de branch, push, PR; na de merge `/opsx:archive`.
