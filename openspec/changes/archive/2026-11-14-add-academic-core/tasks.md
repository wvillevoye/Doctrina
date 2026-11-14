# Taken

## 1. Module opzetten

- [x] 1.1 Maak `src/Modules/Academic/Doctrina.Modules.Academic` (Razor class library) en `.Contracts` volgens `docs/ontwikkelen/modules.md`, met `AcademicModule` (naam `Academic`, permissies `Academic.Read`, `Academic.Import`, `Academic.MyCourses`, menu-items "Academic" en "My courses", health check), `AcademicDbContext` met schema `academic` en fouttitels `AcademicErrors.resx`/`.en.resx`; registreer in `KnownModules`, zet aan en geef `Academic.MyCourses` aan `employee` en `student`, beide in `appsettings.Development.json` (design D8); controleer met `dotnet build` en de bestaande architectuurtests (modulegrenzen) dat alles groen is
- [x] 1.2 Maak testproject `tests/Modules/Doctrina.Modules.Academic.Tests` (design D1) met een LocalDB-fixture uit `Doctrina.TestKit`; controleer met één rooktest dat de module migreert naar een lege database en het schema `academic` aanmaakt
- [x] 1.3 Werk `src/Modules/README.md` bij (eerste productmodule) in het Engels; controleer dat de verwijzing naar `add-academic-core` als "nog te bouwen" weg is

## 2. Datamodel

- [x] 2.1 Maak de entiteiten `Organization`, `AcademicSession`, `Course`, `CourseOffering`, `Person`, `Association` en `PersonLogin` met configuraties en de eerste migratie (design D2, D7: unieke indexen op `ExternalId` + `Source`, op persoon + uitvoering + rol, en op `Issuer` + `Oid`); controleer met test `HetKernmodelVolgtOoapiV5.UitvoeringMetCursusPeriodeEnInschrijving`

## 3. Validatie en upsert

- [x] 3.1 Maak per type een invoerrecord, de kolomlijst (D5) en een validator (D3); controleer met tests `InvoerWordtGevalideerdMetStabieleFoutcodes.VerplichtVeldOntbreekt`, `…EinddatumVoorBegindatum` en `InschrijvingenHebbenEenRol.OnbekendeRol`, plus unit-tests voor de enums (`organizationType`, `academicSessionType`) en `studyLoad`
- [x] 3.2 Maak de upsert-service met het omzetten van verwijzingen binnen dezelfde bron (D3, D4) en resultaat `Created`/`Updated`/`Unchanged`; controleer met tests `OpslaanIsEenUpsertOpExternalIdEnSource.NieuwRecord`, `…BestaandRecordBijwerken`, `…OngewijzigdRecord` (met `FakeTimeProvider`), `VerwijzingenLopenViaExternalIdBinnenDezelfdeBron.InschrijvingVoorEenOnbekendePersoon` en `StatusTeVerwijderenVerbergtEenRecordZonderHetTeWissen.InschrijvingOpTeVerwijderen`, en een test dat een tweede inschrijving met dezelfde persoon, uitvoering en rol een validatiefout geeft
- [x] 3.3 Vul `AcademicErrors.resx`/`.en.resx` met de titels van alle foutcodes van de module; controleer met een test dat elke code uit de module een Engelse titel heeft

## 4. CSV-import

- [x] 4.1 Maak de CSV-lezer met `TextFieldParser` (D5: scheidingsteken uit de kopregel, BOM, kolommen op naam, verplichte en onbekende kolommen, grens op regels en grootte); controleer met unit-tests `HetCsvFormaatIsVergevingsgezindEnVoorspelbaar.PuntkommaAlsScheidingsteken`, `…VerplichteKolomOntbreekt`, `…OnbekendeKolom` en `DeImportHeeftEenGrens.TeGrootBestand`, plus aanhalingstekens met een scheidingsteken en een regeleinde binnen een veld
- [x] 4.2 Maak de importservice die de regels via de upsert-service haalt en het rapport opbouwt (D6); controleer met LocalDB-tests `HetRapportToontHetResultaatPerRegel.EenFouteRegelTussenGoede` en `ImporterenIsIdempotent.ZelfdeBestandOpnieuw`
- [x] 4.3 Maak de pagina `/academic/import` (type, bron met standaard "SIS", `InputFile`, samenvatting en `DcTable` per regel, `Academic.Import`); controleer met integratietests `BeheerdersImporterenEenCsvBestandPerType.PersonenImporteren` en `…ImportZonderPermissie`, en een bUnit- of integratietest dat het rapport de foutcode toont

## 5. Beheerschermen

- [x] 5.1 Maak `/academic` en de lijsten per type (D8: statisch, GET-formulier met `q` en `page`, 25 per pagina, `Status` en `Source` zichtbaar); controleer met integratietests `BeheerdersZienPerTypeEenLijstMetZoeken.ZoekenOpExternalId`, `…Paginering` en `…GeenPermissie`
- [x] 5.2 Maak de detailpagina's met verbanden als links, en bij een persoon de vastgelegde koppelingen (issuer + `oid`); controleer met integratietests `EenDetailpaginaToontEenRecordMetZijnVerbanden.UitvoeringMetInschrijvingen` en `DeBeheerschermenZijnAlleenLezen.GeenBewerkknoppen`

## 6. Koppeling en "My courses"

- [x] 6.1 Maak `CurrentPerson` (D7) en zorg dat de stub-provider in `Doctrina.TestKit` een `oid`-claim meegeeft; controleer met integratietests `EenLoginWordtViaPersonIdAanEenPersoonGekoppeld.EersteGebruikDoorEenGeimporteerdePersoon`, `…VolgendeLoginZonderPersonId`, `InloggenMaaktNooitEenPersoonAan.LoginZonderBijbehorendePersoon` en `…PersoonOpTeVerwijderen`
- [x] 6.2 Maak de pagina `/my-courses` (cursus, uitvoering, periode, rol met Engelse naam; lege toestand; melding zonder persoon); controleer met integratietests `MyCoursesToontDeEigenActieveUitvoeringen.StudentMetTweeUitvoeringen`, `…InschrijvingenEnUitvoeringenDieNietMeetellen`, `…GeenInschrijvingen`, `…MenuItemVoorStudenten` en `…AnoniemeBezoeker`

## 7. Dev-stack en documentatie

- [x] 7.1 Breid `dev-stack/scripts/generate-realm.ps1` uit met `dev-stack/data/*.csv` (D9) en draai het opnieuw; controleer dat de zes bestanden met de importpagina foutloos importeren (volgorde: organizations, academic-sessions, courses, course-offerings, persons, associations) en dat `student`, `docent` en `beheerder` dezelfde `personId` hebben als in de realm
- [x] 7.2 Beschrijf het importeren van de testdata en de nieuwe pagina's in `docs/ontwikkelen/starten.md` en `dev-stack/README.md` (nieuwe tekst in het Engels); controleer dat de stappen letterlijk werken op een lege database

## 8. Afronding

- [x] 8.1 Draai `dotnet test` voor de hele solution en `openspec validate add-academic-core --strict`; controleer dat alles slaagt en dat elk scenario uit de spec-delta een test heeft (lijst in de PR-beschrijving)
- [x] 8.2 Handmatig met de dev-stack: de zes CSV's importeren als `beheerder`, de lijsten en een uitvoering bekijken, daarna als `student` en als `docent` "My courses" openen; controleer dat elk scherm Engels is en de juiste uitvoeringen en rollen toont

## Vervolg in de workflow

- Beoordeel de change vóór `/opsx:apply`.
- Bouwen op een branch die voortbouwt op `feature/switch-ui-to-english` (of na de merges in `main`).
- Archiveren pas na `add-design-system`, `add-idp-unavailable-notice` en `switch-ui-to-english`.
