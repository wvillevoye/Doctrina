# Voorstel

## Why

Docenten willen externe tools (quiz, video, oefenomgeving) vanuit hun cursus starten, zonder dat studenten opnieuw
inloggen. LTI 1.3 is daarvoor de standaard (`docs/Doctrina-voorstel.md` §6.5; huisregel 7: standaard eerst). Nu
`add-content` een contentstructuur heeft, kan een tool als item in een sectie staan. Deze change is rij 8 van de roadmap
(§8, `add-lti-platform`), beperkt tot de **core**: Deep Linking, AGS en NRPS volgen als eigen changes (keuze van de
gebruiker, 2026-11-14), omdat elk daarvan een eigen protocol, eigen sleutels voor de tool en eigen tests vraagt.

## What Changes

- **Nieuwe module `Lti`** (schema `lti`, `DependsOn` Academic en Content) die Doctrina een LTI 1.3-**platform** maakt:
  - **Toolregistratie** voor beheerders (`/lti/tools`, permissie `Lti.Manage`): naam, login-URL, launch-URL,
    redirect-URI's en per tool welke persoonsgegevens meegaan (naam, e-mail; standaard geen). Doctrina geeft elke tool
    een `client_id` en `deployment_id` en toont de platformgegevens die de tool nodig heeft (issuer, authenticatie-endpoint,
    JWKS-URL).
  - **Eigen JWKS-endpoint** (`/lti/jwks.json`) met RS256-sleutels en **sleutelrotatie** ("Rotate key").
  - **Launch** (`LtiResourceLinkRequest`): OIDC third-party login initiation, authenticatieverzoek van de tool en een
    ondertekende `id_token` (JWT) per formulier-POST (`form_post`) naar de redirect-URI van de tool, met de verplichte
    claims, resource link, context (de uitvoering), rollen en optioneel naam en e-mail.
- **Nieuwe itemsoort "tool link"** in de contentstructuur: een docent voegt een link naar een geregistreerde tool toe
  aan een sectie; deelnemers starten de tool met "Launch" (in een nieuw venster). Publiceren, verbergen en volgorde
  werken zoals bij andere items.
- **Modulesysteem**: een module kan naast `/api/v1` ook **browser-endpoints** leveren onder `/{modulenaam}/…`, met de
  inlogcookie (nu kan dat niet; `IModule.MapEndpoints` geeft alleen de bearer-groep). Standaard vereist ze inloggen; een
  endpoint kan zichzelf expliciet anoniem maken (nodig voor de JWKS).
- **Nieuwe contractprojecten** `Doctrina.Modules.Content.Contracts` (een item opzoeken met de leesrechten van de
  gebruiker) en `Doctrina.Modules.Lti.Contracts` (de lijst van geregistreerde tools, voor het formulier in Content).
- De roadmap in `docs/Doctrina-voorstel.md` §8 wordt bijgewerkt: rij 8 wordt "LTI 1.3 core", met vervolgrijen voor
  Deep Linking, AGS en NRPS.

## Buiten de scope

- **Deep Linking 2.0**, **AGS** (cijfers terug) en **NRPS** (deelnemerslijst), en dus ook het controleren van
  JWT's van de tool en het JWKS-adres van de tool in de registratie: elk een eigen vervolgchange.
- Dynamic Registration, tools per uitvoering of per faculteit aan- en uitzetten (elke geregistreerde tool is voor elke
  uitvoering beschikbaar), en meerdere deployments per tool.
- Doctrina als LTI-**tool** (`add-lti-tool-role`), LTI 1.1 (verouderd, §5) en launches in een iframe (alleen een nieuw
  venster).
- Het doorgeven van `custom`-parameters, `tool_platform`-gegevens en de 1EdTech-conformancesuite.
- Het persistent maken van de Data Protection-sleutelring in productie (ingericht door beheer; zie risico's in design).

## Capabilities

### New Capabilities
- `lti-tool-registration`: tools registreren en beheren, per tool de meegegeven persoonsgegevens, de platformgegevens
  voor de tool, en de eigen ondertekeningssleutels met JWKS en rotatie.
- `lti-launch`: tool links in een sectie, en het starten van een tool: login initiation, authenticatieverzoek,
  `id_token` met claims en rollen.

### Modified Capabilities
- `module-system`: een nieuwe eis voor browser-endpoints van een module (`MapBrowserEndpoints`).

Voor de contentstructuur (`course-content`, nog niet gearchiveerd) staat de nieuwe itemsoort in `lti-launch`;
`add-content`, `add-groups` en eerdere changes moeten dus eerst gearchiveerd zijn voordat deze change kan worden
gearchiveerd.

## Impact

- Nieuwe projecten `src/Modules/Lti/Doctrina.Modules.Lti`, `src/Modules/Lti/Doctrina.Modules.Lti.Contracts` en
  `src/Modules/Content/Doctrina.Modules.Content.Contracts`; testproject `tests/Modules/Doctrina.Modules.Lti.Tests`;
  integratietests in `Doctrina.Host.IntegrationTests`.
- `Doctrina.Core`: `IModule.MapBrowserEndpoints` (standaardimplementatie die niets doet, dus bestaande modules
  veranderen niet); `Doctrina.Infrastructure` en `Doctrina.Host`: de endpoints koppelen in `Program.cs`.
- Module Content: nieuwe itemsoort `ToolLink`, kolom `ToolId` in `content.Items` (migratie), formulier en knop "Launch".
- Package: `Microsoft.IdentityModel.JsonWebTokens` (MIT, officieel Microsoft) voor het ondertekenen van de `id_token`;
  geen andere nieuwe packages.
- Configuratie: `Lti:Issuer` (stabiele issuer-URL) en `Lti:Keys:RetireAfterDays`; module aanzetten in
  `appsettings.Development.json`.
- Database: nieuw schema `lti` (`Tools`, `Keys`) en één nieuwe kolom in `content.Items`.
- Documentatie: `docs/ontwikkelen/modules.md` (browser-endpoints), `docs/ontwikkelen/starten.md` (een tool proberen),
  `docs/Doctrina-voorstel.md` §8, `src/Modules/README.md`.
