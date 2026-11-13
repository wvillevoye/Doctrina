# Spec-delta

## MODIFIED Requirements

### Requirement: Rollen worden via configuratie aan permissies gekoppeld
De instelling `Authorization:RolePermissions` SHALL waarden van de `roles`-claim aan permissies koppelen. Een aanroeper
SHALL de vereniging van de permissies van al zijn rollen hebben. De waarde `*` SHALL alle permissies van ingeschakelde
modules geven. Een koppeling naar een permissie van een bekende maar uitgeschakelde module SHALL genegeerd worden, zodat
een module uitzetten geen aanpassing van de koppeling vraagt.

#### Scenario: Permissies uit meerdere rollen worden samengevoegd
- **GIVEN** rol `employee` is gekoppeld aan `Sample.Read` en rol `admin` aan `Sample.Manage`
- **WHEN** een gebruiker met rollen ["employee", "admin"] inlogt
- **THEN** de gebruiker heeft `Sample.Read` en `Sample.Manage`

#### Scenario: Jokerteken geeft elke permissie
- **GIVEN** rol `admin` is gekoppeld aan `*`
- **WHEN** een gebruiker met rol `admin` inlogt
- **THEN** de gebruiker heeft elke permissie die ingeschakelde modules declareren

#### Scenario: Niet-gekoppelde rol geeft niets
- **WHEN** een gebruiker inlogt wiens enige rol niet in de koppeling voorkomt
- **THEN** de gebruiker is ingelogd maar heeft geen permissies

#### Scenario: Koppeling naar een onbekende permissie laat het opstarten mislukken
- **GIVEN** de koppeling verwijst naar permissie `Unknown.Do`, die geen enkele bekende module declareert
- **WHEN** de host start
- **THEN** het opstarten mislukt met een fout die `Unknown.Do` noemt en de rol die ernaar verwijst

#### Scenario: Koppeling naar een permissie van een uitgeschakelde module wordt genegeerd
- **GIVEN** de module Academic is bekend maar niet ingeschakeld, en rol `student` is gekoppeld aan `Academic.MyCourses`
- **WHEN** de host start en een gebruiker met rol `student` inlogt
- **THEN** de host start en de gebruiker heeft geen permissies
