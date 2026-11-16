# Taken

## 1. Gedeelde onderdelen

- [x] 1.1 Verhuis `Downloads` en `download.js` van de module Academic naar `Doctrina.UI` en voeg een variant toe die een `Stream` aanneemt (design D6); laat de importpagina's van Academic de gedeelde versie gebruiken; controleer dat de bestaande bUnit- en integratietests van de import groen blijven en dat een bUnit-test in `Doctrina.UI.Tests` de JS-aanroep met bestandsnaam ziet
- [x] 1.2 Voeg `CurrentRolesAsync` toe aan `Doctrina.Modules.Academic.Contracts` (D4) en laat `GroupsAccess` haar gebruiken; verhuis de in-memory `IAcademicDirectory` uit `Doctrina.Modules.Groups.Tests` naar `Doctrina.TestKit`; controleer met unit-tests dat de methode de rollen geeft voor een docent, een student, iemand zonder inschrijving en een gebruiker zonder persoon, en dat de tests van Groups en de architectuurtests groen blijven

## 2. Module Content opzetten

- [x] 2.1 Maak `src/Modules/Content/Doctrina.Modules.Content` met `ContentModule` (naam `Content`, `DependsOn` Academic, permissies `Content.Manage` en `Content.Read`, health check, fouttitels `ContentErrors.resx`/`.en.resx`), `ContentDbContext` met schema `content`, de entiteiten uit D2 en de eerste migratie; registreer in `KnownModules`, zet aan in `appsettings.Development.json` en voeg `Content:Files` toe aan de appsettings (D5) en `App_Data/` aan `.gitignore`; controleer met `dotnet build`, de architectuurtests en een rooktest in een nieuw testproject `tests/Modules/Doctrina.Modules.Content.Tests` dat de module migreert naar schema `content`

## 3. Secties, items, publiceren en volgorde

- [x] 3.1 Maak `ContentService` voor secties (aanmaken, hernoemen, verwijderen met haar items; D2, D3) met foutcodes `Content.Section.*` en vertalingen; controleer met LocalDB-tests `DocentenDelenDeContentVanEenUitvoeringInSectiesIn.DocentMaaktEenSectie`, `…SectieZonderTitel` en `…SectieMetItemsVerwijderen`, en dat `ExternalId` gelijk is aan het `Id` en `Source` "Doctrina" is
- [x] 3.2 Voeg items toe: link en pagina aanmaken, hernoemen en verwijderen, met de controle op het webadres; controleer met tests `EenSectieBevatPaginasBestandenEnLinks.LinkToevoegen`, `…LinkZonderGeldigAdres` en `…ItemVerwijderen`
- [x] 3.3 Voeg publiceren en verbergen toe en `SectionsAsync(offeringId, includeDrafts)` (D3); controleer met tests `ContentIsPasZichtbaarNaPubliceren.NieuwItemIsEenConcept`, `…GepubliceerdItemInEenVerborgenSectie` en `…ItemVerbergen`
- [x] 3.4 Voeg verplaatsen toe (D2); controleer met tests `DocentenBepalenDeVolgordeVanSectiesEnItems.ItemOmhoogVerplaatsen` en `…SectieOmlaagVerplaatsen`, plus dat het eerste item omhoog verplaatsen niets wijzigt en dat een verwijderd item wordt overgeslagen
- [x] 3.5 Maak `ContentAccess` met `CanManageAsync` en `CanReadAsync` (D4); controleer met unit-tests voor lecturer, coordinator, teaching-assistant, student, docent van een andere uitvoering, `Content.Manage` en `Content.Read`

## 4. Markdown en bestanden

- [x] 4.1 Voeg Markdig (laatste stabiele versie) toe aan `Directory.Packages.props` en maak `Markdown.ToSafeHtml` en `MarkdownView` (D7); controleer met unit-tests `PaginasWordenInMarkdownGeschrevenEnVeiligWeergegeven.OpmaakWordtWeergegeven` en `…HtmlEnScriptsWordenNietUitgevoerd`, plus een `mailto:`-link die blijft werken en een afbeelding met een `data:`-adres die wordt leeggemaakt
- [x] 4.2 Maak `ContentFileStore` en het toevoegen, openen en verwijderen van een bestand in `ContentService` (D5); controleer met tests met een tijdelijke map `BestandenWordenGeuploadEnGedownload.BestandUploadenEnDownloaden` (zelfde inhoud en naam terug) en `…BestandTeGroot` (geen item en geen bestand achtergebleven), dat verwijderen het bestand van schijf haalt, dat een bestandsnaam met een pad (`..\..\x.pdf`) alleen als `x.pdf` wordt bewaard en dat een ontbrekend bestand `Content.File.Missing` geeft

## 5. Schermen

- [x] 5.1 Maak de sectie "Content" en registreer haar met `AddOfferingPageSection` op volgorde 10 (D8); controleer met integratietests `DeelnemersZienDeGepubliceerdeContentOpDePaginaVanDeUitvoering.StudentZietGepubliceerdeContent`, `…UitvoeringZonderContent` en `…DocentZietConcepten`
- [x] 5.2 Maak de beheerpagina `/courses/{offering}/content` (interactief, D8); controleer met integratietests `AlleenDocentenVanDeUitvoeringEnBeheerdersBeherenContent.StudentMagGeenContentBeheren`, `…DocentVanEenAndereUitvoering` en `…BeheerderZonderInschrijving`, en met bUnit-tests dat een sectie, link en bestand toevoegen, publiceren, verplaatsen en verwijderen via de pagina de service aanroepen en het resultaat tonen (inclusief de melding bij een te groot bestand)
- [x] 5.3 Maak de itempagina `/courses/{offering}/content/{item}` met de downloadknop en de bewerkpagina `…/edit` (D6, D8); controleer met integratietests `ContentIsPasZichtbaarNaPubliceren.StudentOpentEenVerborgenPaginaRechtstreeks`, `AlleenDocentenVanDeUitvoeringEnBeheerdersBeherenContent.DocentOpentEenConcept`, `DeelnemersZienDeGepubliceerdeContentOpDePaginaVanDeUitvoering.NietIngeschrevenStudentOpentEenPagina` en `…LezenMetContentReadZonderInschrijving`, en met bUnit-tests `PaginasWordenInMarkdownGeschrevenEnVeiligWeergegeven.VoorbeeldTijdensHetBewerken` en dat "Download" de gedeelde download aanroept met de bestandsnaam
- [x] 5.4 Werk `src/Modules/README.md` bij en beschrijf in `docs/ontwikkelen/starten.md` (Engels) hoe je content probeert met de testdata, en dat de map `Content:Files:Path` mee moet in de back-up; controleer dat de stappen kloppen met de pagina's

## 6. Afronding

- [x] 6.1 Draai `dotnet test` voor de hele solution en `openspec validate add-content --strict`; controleer dat alles slaagt en dat elk scenario uit de spec-delta een test heeft (lijst in de PR-beschrijving)
- [x] 6.2 Handmatig met de dev-stack en de geïmporteerde testdata: als `docent` in "Cell biology" een sectie met een pagina, een link en een bestand maken, publiceren en de volgorde wijzigen; daarna als `student` de content zien, de pagina openen en het bestand downloaden; controleer dat een concept voor de student onzichtbaar is en dat alles Engels is

## Vervolg in de workflow

- Beoordeel de change vóór `/opsx:apply`.
- Bouwen op `feature/add-content` (voortbouwend op `feature/add-bulk-import`).
- Archiveren pas na `add-groups` en de changes daarvóór.
