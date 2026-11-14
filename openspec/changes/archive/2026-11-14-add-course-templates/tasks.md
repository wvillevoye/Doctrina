# Taken

## 1. Contract in Academic

- [x] 1.1 Voeg `IAcademicDirectory.SiblingOfferingsAsync` toe aan `Doctrina.Modules.Academic.Contracts`, implementeer hem in `AcademicDirectory` (actieve uitvoeringen van dezelfde cursus zonder de uitvoering zelf, nieuwste periode eerst, binnen de bestaande `SerializedAsync`) en in `FakeDirectory` (lijst `Siblings`) (D2); controleer met een LocalDB-test `ContentKopierenNaarEenLegeUitvoering.AcademicGeeftDeAndereUitvoeringenVanDezelfdeCursus` in de Academic-tests en `dotnet build` van de hele solution

## 2. Kopiëren in Content

- [x] 2.1 Voeg `ContentFileStore.Copy` toe (D6) en `ContentService.CopyFromAsync` met `CopyResult`, de controles in de volgorde van D4, de foutcodes `Content.Copy.InvalidSource` en `Content.Copy.TargetNotEmpty` (met titels in `ContentErrors.resx`/`.en.resx` en in `ContentErrors.All`) en het kopiëren van secties en items volgens D3 en D5; controleer met LocalDB-tests `ContentKopierenNaarEenLegeUitvoering.DocentKopieertDeContentVanEenEerdereUitvoering`, `…GekopieerdeContentIsEenConceptEnOnzichtbaarVoorStudenten`, `…BestandenWordenGekopieerdEnZijnLosVanDeBron`, `…DeBronBlijftOngewijzigd`, `…VerwijderdeItemsGaanNietMee`, `…ToolLinkBehoudtDeTool`, `…ToolLinkNaarEenVerwijderdeToolWordtOvergeslagen` en `…OntbrekendBestandWordtOvergeslagen`
- [x] 2.2 Controleer de voorwaarden en rechten met tests `ContentKopierenNaarEenLegeUitvoering.UitvoeringMetContentWordtNietOverschreven`, `…EenUitvoeringMetAlleenVerwijderdeSectiesTeltAlsLeeg`, `…BronVanEenAndereCursusWordtGeweigerd`, `…ZonderBeheerrechtOpDeBronWordtNietsGekopieerd` en `…ZonderBeheerrechtOpDeNieuweUitvoeringWordtNietsGekopieerd` (bron = doel en onbekende bron vallen onder `Content.Copy.InvalidSource`)
- [x] 2.3 Controleer dat een mislukte kopie niets achterlaat (D6) met de test `ContentKopierenNaarEenLegeUitvoering.KopierenIsAllesOfNiets` (het tweede bronbestand wordt exclusief geopend, zodat het kopiëren mislukt)

## 3. Beheerpagina

- [x] 3.1 Voeg de kaart "Copy content" toe aan `ManageContent.razor` (D7): keuzelijst "Copy from", knop met bezig-toestand, melding "Copied N sections and M items as drafts." (met "K items could not be copied."), alleen als er geen actieve secties zijn en er andere uitvoeringen zijn; controleer met een bUnit-test en een integratietest `ContentKopierenNaarEenLegeUitvoering.DeKaartVerschijntAlleenAlsKopierenKan` (kaart aanwezig; niet bij een uitvoering met een sectie; niet zonder andere uitvoeringen) en een integratietest die via de pagina kopieert en de nieuwe concepten controleert

## 4. Documentatie en afronding

- [x] 4.1 Werk `docs/ontwikkelen/starten.md` (Engels: een uitvoering vullen uit een eerdere, wat wel en niet meegaat) en `docs/Doctrina-voorstel.md` §6.3 (de open vraag is voor de eerste stap beantwoord: kopiëren uit een eerdere uitvoering; contentblok-templates later) bij; controleer met `dotnet build` zonder waarschuwingen
- [x] 4.2 Draai `dotnet test` voor de hele solution en `openspec validate add-course-templates --strict`; controleer dat alles slaagt en dat elk scenario uit de spec-delta een test heeft (lijst in de PR-beschrijving)
- [x] 4.3 Handmatig met de dev-stack: als `docent` in een tweede uitvoering van dezelfde cursus (of een door de CSV-testdata gemaakte tweede uitvoering) "Copy content" gebruiken, controleren dat alles als concept staat, bestanden te downloaden zijn en de bron ongemoeid is; stop de host daarna
