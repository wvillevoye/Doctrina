# Taken

## 1. Contracten van Academic en de uitvoeringspagina

- [x] 1.1 Vul `Doctrina.Modules.Academic.Contracts` met `IAcademicDirectory`, `OfferingInfo`, `OfferingMember`, `OfferingRole`, `OfferingPageSection` en `AddOfferingPageSection<T>` (design D1), en implementeer `IAcademicDirectory` in Academic op basis van de bestaande queries en `CurrentPerson`; controleer met LocalDB-tests in `Doctrina.Modules.Academic.Tests` dat `FindOfferingAsync` een te verwijderen uitvoering als `NotFound` geeft en dat `MembersAsync` alleen actieve inschrijvingen van actieve personen teruggeeft, met de juiste rol; de architectuurtests (modulegrenzen) blijven groen
- [x] 1.2 Maak de pagina `/courses/{offering}` in Academic met de secties via `DynamicComponent` (design D2, D8); voeg aan `Doctrina.TestKit` een testmodule toe die een sectie "Test section" registreert; controleer met integratietests `ElkeUitvoeringHeeftEenPaginaVoorHaarDeelnemers.StudentOpentDePaginaVanZijnUitvoering`, `…NietIngeschreven`, `…UitvoeringOpTeVerwijderen` en `ModulesVoegenSectiesToeAanDePaginaVanEenUitvoering.SectieVanEenIngeschakeldeModule`, plus een test dat `Academic.Read` de pagina zonder inschrijving opent
- [x] 1.3 Laat elke rij in "My courses" linken naar `/courses/{offering}`; controleer met integratietest `MyCoursesLinktNaarDePaginaVanElkeUitvoering.LinkNaarDeUitvoering` en dat de bestaande tests van "My courses" groen blijven

## 2. Module Groups opzetten

- [x] 2.1 Maak `src/Modules/Groups/Doctrina.Modules.Groups` en `.Contracts` (met `GroupMembershipChanged` en `MembershipChange`), `GroupsModule` (naam `Groups`, `DependsOn` Academic, permissie `Groups.Manage`, health check, fouttitels `GroupsErrors.resx`/`.en.resx`), `GroupsDbContext` met schema `groups`, de entiteiten uit design D3 en de eerste migratie; registreer in `KnownModules` en zet aan in `appsettings.Development.json`; controleer met `dotnet build`, de architectuurtests en een rooktest in een nieuw testproject `tests/Modules/Doctrina.Modules.Groups.Tests` dat de module migreert naar schema `groups`
- [x] 2.2 Voeg aan het testproject een in-memory `IAcademicDirectory` toe (uitvoering, leden met rol, huidige persoon) zodat de servicetests zonder Academic-database draaien; controleer dat de rooktest het gebruikt

## 3. Groepen en indelen

- [x] 3.1 Maak `GroupService` voor sets en groepen (aanmaken, hernoemen, verwijderen met status `ToBeDeleted` en het verwijderen van de leden; design D3, D4) met foutcodes `Groups.*` en vertalingen; controleer met tests `DocentenBeherenGroupSetsEnGroepenInHunUitvoering.DocentMaaktEenGroupSetMetGroepen` en `…GroepMetLedenVerwijderen`, en dat `ExternalId` gelijk is aan het `Id` en `Source` "Doctrina" is
- [x] 3.2 Voeg handmatig indelen toe (toevoegen, verplaatsen, eruit halen, maximale groepsgrootte, alleen studenten met een actieve inschrijving) met events op de groep (D4); controleer met tests `StudentenWordenHandmatigIngedeeld.StudentToevoegen`, `…StudentVerplaatsen`, `…GroepIsVol`, `…PersoonZonderStudentinschrijving` en `WijzigingenInGroepslidmaatschapWordenAlsEventGepubliceerd.VerplaatsenGeeftTweeEvents` (met een vastleggende eventhandler)
- [x] 3.3 Voeg willekeurig verdelen toe (D6); controleer met tests `StudentenWordenWillekeurigVerdeeld.VerdelenOverDrieGroepen`, `…GroepenVanVier` en `…AlIngedeeldeStudentenBlijven`, plus een test dat verdelen boven de maximale groepsgrootte wordt geweigerd zonder iets op te slaan
- [x] 3.4 Maak `GroupsAccess.CanManageAsync` (D5); controleer met unit-tests voor lecturer, coordinator, teaching-assistant, student, docent van een andere uitvoering en `Groups.Manage`

## 4. Pagina's van Groups

- [x] 4.1 Maak de sectie "My groups" en registreer haar met `AddOfferingPageSection` (D8); controleer met integratietests `StudentenZienHunGroepenOpDePaginaVanDeUitvoering.ZichtbareEnVerborgenGroupSet` en `…DocentZietDeLinkNaarHetGroepsbeheer`, en dat een student zonder groepen "You are not in any groups yet." ziet
- [x] 4.2 Maak de beheerpagina `/courses/{offering}/groups` (interactief, D8) met dezelfde check als 3.4; controleer met integratietests `AlleenDocentenVanDeUitvoeringEnBeheerdersBeherenGroepen.StudentMagGeenGroepenBeheren`, `…DocentVanEenAndereUitvoering` en `…BeheerderZonderInschrijving`, en met bUnit-tests dat aanmaken, indelen en willekeurig verdelen via de pagina de service aanroepen en het resultaat tonen (inclusief de melding bij een volle groep)

## 5. Documentatie en afronding

- [x] 5.1 Werk `src/Modules/README.md` bij en beschrijf in `docs/ontwikkelen/starten.md` (Engels) hoe je groepen probeert met de testdata: als `docent` naar een uitvoering, groepen maken en verdelen, daarna als `student` "My groups" zien; controleer dat de stappen kloppen met de pagina's
- [x] 5.2 Draai `dotnet test` voor de hele solution en `openspec validate add-groups --strict`; controleer dat alles slaagt en dat elk scenario uit de spec-delta een test heeft (lijst in de PR-beschrijving)
- [x] 5.3 Handmatig met de dev-stack en de geïmporteerde testdata: als `docent` in "Cell biology (2026-P1)" een zichtbare GroupSet maken en de studenten over 3 groepen verdelen, één student verplaatsen; daarna als `student` de uitvoeringspagina openen; controleer dat alles Engels is en klopt

## Vervolg in de workflow

- Beoordeel de change vóór `/opsx:apply`.
- Bouwen op `feature/add-groups` (voortbouwend op `feature/add-academic-core`).
- Archiveren pas na `add-academic-core` en de changes daarvóór.
