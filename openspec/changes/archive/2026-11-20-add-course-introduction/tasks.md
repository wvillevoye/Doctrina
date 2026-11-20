# Taken

## 1. Contract: zichtbaarheid van een sectie

- [x] 1.1 Voeg aan `OfferingPageSection` (`Doctrina.Modules.Academic.Contracts/AcademicDirectory.cs`) de optionele parameter `Func<IServiceProvider, ClaimsPrincipal, Guid, Task<bool>>? IsVisible = null` toe en geef `AddOfferingPageSection` een bijbehorende optionele parameter, met Engelse XML-doc; controleer dat de hele solution compileert zonder aanpassing van bestaande aanroepen
- [x] 1.2 Laat `CoursePage.razor` per sectie `IsVisible` aanroepen (scope van de pagina) en sectie én menu-item weglaten bij `false`; controleer dat de bestaande tests van `ModulesVoegenSectiesToeAanDePaginaVanEenUitvoering` slagen

## 2. Gegevens

- [x] 2.1 Voeg in `Domain/Entities.cs` `IntroductionTemplate` (`Name`, `Layout`, `StartBody`, `Position`, `IsDefault`) en `CourseIntroduction` (`OfferingId`, `TemplateId`, `Body`, `Published`) toe als `ExchangeableEntity`, plus `enum IntroductionLayout { Plain, Banner, Objectives }` opgeslagen als tekst
- [x] 2.2 Configureer ze in `ContentDbContext` (unieke index op `OfferingId`, maximale lengtes, geen cascade) en maak de migratie `AddCourseIntroduction`; het sjabloon "Default" (`plain`, lege startinhoud, `IsDefault`) maakt `IntroductionService` bij eerste gebruik aan, omdat een migratie de door EF gegenereerde sequentiële Id (huisregel 5) niet kan leveren; controleer dat de migratie op een lege LocalDB-database slaagt

## 3. Service

- [x] 3.1 Voeg `IntroductionService` toe in `Application/` (scoped, geregistreerd in `ContentModule`): lezen (met terugval op "Default" bij een ontbrekend of inactief sjabloon), opslaan (tekst ≤ 50.000 tekens, `Result` met veldfout "Text"), sjablonen lijst/aanmaken/wijzigen/verwijderen (naam verplicht en ≤ 100 tekens, "Default" niet te verwijderen, zacht verwijderen)
- [x] 3.2 Voeg de foutcodes `Content.Introduction.Invalid`, `Content.Template.Invalid`, `Content.Template.NotFound` en `Content.Template.DefaultProtected` toe aan `ContentErrors` en `Resources/ContentErrors.resx` (Nederlandse titel in `ContentErrors.resx`, Engelse in `ContentErrors.en.resx`, zoals de bestaande codes); controleer dat de bestaande controle op foutcodes slaagt

## 4. Componenten

- [x] 4.1 Schrijf `IntroductionSection.razor` met scoped CSS voor `intro--plain`, `intro--banner` en `intro--objectives`, uitsluitend met `--dc-*`-tokens, hergebruik `MarkdownView`, toon `DcBadge` "Draft" voor beheerders en een link "Edit introduction" voor wie kan beheren; controleer dat de architectuurtest op letterlijke kleuren slaagt
- [x] 4.2 Registreer de sectie in `ContentModule` met `order: 5`, `title: "Introduction"`, een icoon uit `DcIconSet` (voeg zo nodig één Lucide-icoon toe volgens `Icons/VERSION`) en een `IsVisible` die `true` geeft voor een gepubliceerde introductie of voor wie kan beheren (die ook een knop "Add introduction" krijgt als er nog geen is)
- [x] 4.3 Schrijf `Pages/EditIntroductionPage.razor` (`/courses/{OfferingId:guid}/introduction`): keuzelijst "Template", tekstveld "Text", "Published", knoppen "Preview" en "Save" naar het patroon van `EditContentPage`; toegang via `ContentAccess.CanManageAsync`, anders naar `/access-denied`; startinhoud invullen alleen bij een lege tekst
- [x] 4.4 Schrijf `Pages/ManageTemplatesPage.razor` (`/content/templates`, `[RequirePermission(ContentPermissions.Manage)]`): lijst met `DcTable`, formulier voor naam, opmaak en startinhoud, verwijderen met bevestiging
- [x] 4.5 Voeg aan `ContentModule.Navigation` het item "Intro templates" toe (`/content/templates`, `Content.Manage`, `Order: 92`, een bestaand icoon); controleer dat docenten zonder `Content.Manage` het item niet zien

## 5. Tests

- [x] 5.1 Schrijf `DeIntroductieStaatBovenDeContentVanEenUitvoering` met de scenario's `IntroductieBovenDeContent`, `GeenIntroductieVoorDeelnemers` en `ConceptAlleenVoorBeheerders` en `DocentVoegtEenIntroductieToe` (integratietests in `Doctrina.Host.IntegrationTests/Academic`, naast `ContentTests`)
- [x] 5.2 Schrijf `DocentenBewerkenDeIntroductieInMarkdown` met `DocentSchrijftEnPubliceert`, `VoorbeeldTijdensHetBewerken`, `TeLangeTekst` en `StudentBewerktNiet`
- [x] 5.3 Schrijf `DeIntroductieWordtVeiligWeergegeven.ScriptsWordenNietUitgevoerd`
- [x] 5.4 Schrijf `BeheerdersBeherenIntroductiesjablonen` met `BeheerderMaaktEenSjabloon`, `SjabloonZonderNaam`, `StandaardsjabloonBlijftBestaan` en `DocentBeheertGeenSjablonen`
- [x] 5.5 Schrijf `EenUitvoeringKiestEenSjabloonDatDeOpmaakBepaalt` met `StartinhoudBijEenLegeIntroductie`, `SjabloonWisselenBehoudtDeTekst`, `OpmaakVanHetSjabloon` (inclusief controle dat de gecompileerde CSS geen letterlijke kleuren bevat) en `VerwijderdSjabloon`
- [x] 5.6 Schrijf een test voor `IsVisible` in `ModulesVoegenSectiesToeAanDePaginaVanEenUitvoering` (sectie en menu-item verdwijnen bij `false`)

## 6. Afronding

- [x] 6.1 Voeg een korte alinea (Engels) toe aan `docs/ontwikkelen/modules.md` over `IsVisible` en over sjablonen voor secties
- [ ] 6.2 Open de host op een cursus als docent, student en beheerder in beide thema's: controleer volgorde (Introduction vóór Content), menu, bewerken met voorbeeld, een sjabloonwissel en dat het cursusmenu in de zijbalk klopt
- [x] 6.3 Voer `openspec validate add-course-introduction --strict` uit en draai de volledige testset
