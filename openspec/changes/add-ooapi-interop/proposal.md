# Proposal

## Why

Doctrina bewaart het onderwijskernmodel al volgens de OOAPI v5-begrippen, maar externe partijen (SURF/Edu-V-koppelingen,
rooster- en portfolio-systemen) kunnen het alleen lezen via de eigen REST-API onder `/api/v1`, met Doctrina-conventies.
Het voorstel kiest OOAPI als primaire NL-koppelstandaard (§5, §6.7): wie al OOAPI spreekt moet Doctrina kunnen
bevragen zonder maatwerk. Dit is rij 6 van de fasering (§8 van `docs/Doctrina-voorstel.md`); alle afhankelijkheden
(3 `add-academic-core`, 5 `add-api-and-import`) zijn klaar.

## What Changes

- Nieuwe module `Ooapi` (`Doctrina.Modules.Interop.OOAPI`, zoals in het voorstel §3) die **alleen-lezen OOAPI v5-endpoints
  aanbiedt** onder `/ooapi/v5` voor `organizations`, `academic-sessions`, `courses`, `offerings`, `persons` en
  `associations`, met de paginering, filters en antwoordvorm van de standaard.
- De module leest via een nieuw leescontract in `Doctrina.Modules.Academic.Contracts`, niet via de implementatie van Academic.
- Beveiliging: dezelfde bearer-tokens (client credentials) als `/api/v1`, met een nieuwe permissie `Ooapi.Read`.
- De host krijgt een tweede routegroep `/ooapi/v5` (bearer-only, zoals `/api/v1`); `IModule` krijgt daarvoor een optioneel
  `MapOoapiEndpoints`. **BREAKING** voor de eis dat álle module-endpoints onder `/api/v1` staan: standaard-API's mogen
  onder een eigen, door de standaard bepaald prefix.
- Documentatie (Engels) over het koppelen van een OOAPI-consument.

**Buiten de scope:**
- OOAPI **consumeren** (een bron bevragen en de bulk-pipeline vullen): aparte change zodra het bron-SIS en de profielen
  bekend zijn (open vragen §10).
- **Schrijven** via OOAPI (o.a. associations): de bestaande `/api/v1`-API blijft de schrijfroute. Dit wijkt af van §6.7;
  zie design D2.
- OOAPI-resources die Doctrina niet heeft (`programs`, `components`, `groups`, `rooms`, `buildings`, `news-feeds`, …).
- Opname in het OpenAPI-document en de Scalar-pagina van Doctrina (de standaard heeft zijn eigen specificatie).
- Een eigen autorisatiemodel per resource: één permissie `Ooapi.Read`.

## Capabilities

### New Capabilities
- `ooapi-provider`: Doctrina biedt het onderwijskernmodel alleen-lezen aan als OOAPI v5 onder `/ooapi/v5`:
  resources, veldmapping, paginering, filters, foutopmaak en autorisatie.

### Modified Capabilities
- `api-conventions`: de eis dat alle module-endpoints onder `/api/v1` staan, laat een standaard-API onder een eigen
  prefix (`/ooapi/v5`) toe, met dezelfde bearer-only beveiliging.

## Impact

- Nieuw: `src/Modules/Interop/Doctrina.Modules.Interop.OOAPI` (+ testproject), registratie in `KnownModules.cs` en
  `Modules:Enabled`.
- Wijzigt: `Doctrina.Modules.Academic.Contracts` (leescontract), `Doctrina.Modules.Academic` (implementatie van dat
  contract, hergebruik van de bestaande queries), `Doctrina.Core` (`IModule.MapOoapiEndpoints`), `Doctrina.Host`
  (routegroep, `Authorization:RolePermissions` in `appsettings.Development.json`).
- Geen nieuwe tabellen en geen nieuwe packages.
- Documentatie: `docs/doctrina-integratie.md` of `docs/ontwikkelen/`; `docs/Doctrina-voorstel.md` §6.7 en §8 bijwerken.
