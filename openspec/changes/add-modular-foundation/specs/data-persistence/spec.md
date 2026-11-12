# Spec-delta

## Purpose

Geeft elke module een eigen, afgeschermd en zelfstandig gemigreerd deel van de gedeelde SQL Server-database, met
sleutels en wijzigingsvelden die bulkimports, upserts op externe sleutel en delta-synchronisatie ondersteunen.

## ADDED Requirements

### Requirement: Elke module heeft een eigen databaseschema
Elke module die gegevens opslaat SHALL al haar tabellen in een eigen SQL Server-schema opslaan, genoemd naar de module
in kleine letters. Een module SHALL NOT tabellen in het schema van een andere module aanmaken of wijzigen.

#### Scenario: Tabellen worden in het moduleschema aangemaakt
- **GIVEN** een ingeschakelde module `Sample` met een opgeslagen entiteit
- **WHEN** haar migraties op een lege database worden toegepast
- **THEN** de tabel van de entiteit staat in schema `sample` en er staat geen tabel van `Sample` in een ander schema

### Requirement: Elke module migreert zelfstandig
Elke module SHALL een eigen migratiegeschiedenis hebben, opgeslagen in haar eigen schema, zodat modules los van
elkaar gemigreerd, toegevoegd of verwijderd kunnen worden. Alleen ingeschakelde modules SHALL gemigreerd worden.

#### Scenario: Migratiegeschiedenis per module
- **GIVEN** twee ingeschakelde modules met migraties
- **WHEN** de migraties worden toegepast
- **THEN** het schema van elke module bevat een eigen migratiegeschiedenistabel met alleen de migraties van die module

#### Scenario: Uitgeschakelde module wordt niet gemigreerd
- **GIVEN** een module met migraties die niet is ingeschakeld
- **WHEN** de host migraties toepast
- **THEN** het schema van die module wordt niet aangemaakt

### Requirement: Automatisch migreren is instelbaar
De host SHALL openstaande migraties van ingeschakelde modules bij het opstarten alleen toepassen als de instelling
`Database:MigrateOnStartup` true is. De standaardwaarde SHALL true zijn in de omgeving Development en anders false.

#### Scenario: Openstaande migraties toegepast in development
- **GIVEN** de omgeving Development en een database met openstaande migraties
- **WHEN** de host start
- **THEN** alle openstaande migraties van ingeschakelde modules zijn toegepast voordat de host verzoeken accepteert

#### Scenario: Geen automatische migratie buiten development
- **GIVEN** de omgeving Production zonder ingestelde `Database:MigrateOnStartup`
- **WHEN** de host start met openstaande migraties
- **THEN** er wordt geen migratie toegepast

### Requirement: Entiteitsleutels zijn sequentiële GUID's
Entiteit-Id's SHALL als `uniqueidentifier` worden opgeslagen en door de opslaglaag gegenereerd in een volgorde die voor
SQL Server sequentieel is, nooit als willekeurige GUID's aangeleverd door applicatiecode.

#### Scenario: Bij invoegen gegenereerde sleutels zijn oplopend
- **WHEN** 1.000 nieuwe entiteiten zonder Id in één batch worden opgeslagen
- **THEN** elke entiteit heeft een niet-lege Id en de Id's staan in oplopende SQL Server-sorteervolgorde voor `uniqueidentifier`

### Requirement: Uitwisselbare entiteiten hebben synchronisatievelden
Uitwisselbare entiteiten SHALL `Id`, `ExternalId`, `Status`, `DateLastModified` en `Source` hebben. `ExternalId` SHALL
uniek zijn per `Source`. `DateLastModified` SHALL bij elke invoeging en wijziging automatisch in UTC worden gezet.

#### Scenario: DateLastModified wordt automatisch bijgehouden
- **GIVEN** de systeemklok staat op 2026-11-12T10:00:00Z
- **WHEN** een uitwisselbare entiteit wordt ingevoegd en later om 2026-11-12T11:00:00Z gewijzigd
- **THEN** haar `DateLastModified` is 10:00:00Z na het invoegen en 11:00:00Z na de wijziging

#### Scenario: Dubbele ExternalId binnen een bron wordt geweigerd
- **GIVEN** er bestaat een entiteit met `ExternalId` "i6123456" en `Source` "SIS"
- **WHEN** een andere entiteit van hetzelfde type met dezelfde `ExternalId` en `Source` wordt opgeslagen
- **THEN** het opslaan mislukt met een conflict en er bestaat geen tweede record

#### Scenario: Zelfde ExternalId uit een andere bron is toegestaan
- **GIVEN** er bestaat een entiteit met `ExternalId` "i6123456" en `Source` "SIS"
- **WHEN** een entiteit met `ExternalId` "i6123456" en `Source` "Manual" wordt opgeslagen
- **THEN** beide records bestaan
