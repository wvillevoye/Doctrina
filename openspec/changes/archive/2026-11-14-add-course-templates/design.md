# Design

## Context

Zie `proposal.md` voor aanleiding en scope. Waar deze change op voortbouwt (gelezen in de code):

- **Content** (`add-content`): `ContentSection` en `ContentItem` hangen aan een `OfferingId` zonder foreign key; Content
  controleert de uitvoering via `IAcademicDirectory`. `ContentService` bevat alle wijzigingen (één `SaveChanges` per
  methode, `Result` terug); `ContentAccess.CanManageAsync/CanReadAsync` beslist wie mag; bestanden staan op schijf als
  `{root}/{offeringId}/{itemId}` (`ContentFileStore`, D5 van `add-content`) en `AddFileAsync` ruimt het bestand op als het
  opslaan mislukt. Een item heeft `Kind` (`Page`, `File`, `Link`, `ToolLink`); `ToolLink` bewaart een `ToolId` zonder
  foreign key en `ILtiToolCatalog` (Lti.Contracts) kan zeggen of een tool bestaat.
- **Academic**: `Course` heeft meerdere `CourseOffering`s (met een `AcademicSession`). Uitvoeringen ontstaan via de SIS,
  CSV-import of REST-API; er is geen scherm om er een aan te maken. `IAcademicDirectory` kent alleen één uitvoering
  (`FindOfferingAsync`), de persoon, en de leden; `OfferingInfo` bevat geen cursus-id.
- **Voorstel §6.3**: "Een Course dient als template; een CourseOffering wordt daaruit aangemaakt." en de open vraag welk
  soort template bedoeld is.

Keuzes van de gebruiker (2026-11-14): alleen cursussjablonen; de template is een bestaande uitvoering; handmatig toepassen.

## Goals / Non-Goals

**Goals:**
- Een docent vult een nieuwe, lege uitvoering met één klik met de structuur en inhoud van een eerdere uitvoering van
  dezelfde cursus, veilig (als concept, niets overschrijven, de bron blijft ongewijzigd).
- Geen nieuwe tabellen en geen migratie: de kopie gebruikt de bestaande secties en items.

**Non-Goals:** zie "Buiten de scope" in het voorstel (contentblok-templates, uitvoering aanmaken, automatisch kopiëren,
samenvoegen, groepen en deelnemers kopiëren).

## Decisions

### D1: De template is een bestaande uitvoering (afwijking van §6.3)

§6.3 zegt: een `Course` dient als template. Daarvoor zou de content aan een `Course` moeten hangen, met eigen tabellen
en een eigen editor, terwijl content nu per uitvoering wordt beheerd en uitvoeringen uit de SIS komen. Een **eerdere
uitvoering van dezelfde cursus** als bron past bij hoe docenten werken ("zet vorig jaar over"), kost geen nieuw
datamodel en laat een latere echte template-inhoud per cursus open: die kan later dezelfde kopieerlogica gebruiken met een
andere bron. Dit is een besluit van de gebruiker (2026-11-14); `docs/Doctrina-voorstel.md` §6.3 wordt bijgewerkt.

### D2: Contract `SiblingOfferingsAsync` in Academic.Contracts

Content mag Academic niet rechtstreeks bevragen en kent de cursus van een uitvoering niet. Nieuwe methode op
`IAcademicDirectory`:

```csharp
/// De andere actieve uitvoeringen van dezelfde cursus als offeringId, de nieuwste periode eerst.
Task<IReadOnlyList<OfferingInfo>> SiblingOfferingsAsync(Guid offeringId, CancellationToken cancellationToken);
```

Een onbekende of verwijderde uitvoering geeft een lege lijst. Sortering op `AcademicSession.StartDate` aflopend, dan op
naam. `OfferingInfo` blijft ongewijzigd (geen cursus-id nodig), zodat bestaande aanroepers en fakes niet breken; alleen
`AcademicDirectory` en `FakeDirectory` krijgen de nieuwe methode (de `FakeDirectory` krijgt een lijst `Siblings`).

### D3: Wat wordt gekopieerd

`ContentService.CopyFromAsync(user?, targetOfferingId, sourceOfferingId, ct)` leest de **actieve** secties met de
**actieve** items van de bron (inclusief concepten) en maakt in het doel:

- per sectie een nieuwe `ContentSection` (titel, `Position` opnieuw genummerd vanaf 1 in dezelfde volgorde,
  `Published = false`);
- per item een nieuw `ContentItem` met dezelfde `Kind`, titel en volgorde en `Published = false`, plus de velden van de
  soort: `Body` (pagina), `Url` (link), `ToolId` (tool link), `FileName`, `ContentType`, `Size` (bestand).

Nieuwe entiteiten volgen de huisregels: `Id` door EF, `ExternalId` = `Id`, `Source` = `Doctrina`. Er is geen verwijzing
tussen kopie en bron (de kopie is onafhankelijk). Verwijderde (`Status` niet actief) secties en items gaan niet mee.

Alles wordt **als concept** gekopieerd, ook wat in de bron gepubliceerd is: een nieuwe uitvoering mag niet ongemerkt
oude of niet-aangepaste content aan studenten tonen (privacy en juistheid); de docent publiceert bewust.

### D4: Voorwaarden en rechten

Volgorde van de controles in `CopyFromAsync`, zodat de foutcode eenduidig is:

1. De gebruiker mag het **doel** beheren (`ContentAccess.CanManageAsync`) → anders `Content.Access.Forbidden`
   (`IContentDirectory.AccessForbiddenCode`, bestaand).
2. De bron is een andere uitvoering van dezelfde cursus: `SiblingOfferingsAsync(doel)` bevat de bron → anders
   `Content.Copy.InvalidSource` (validatie, 400); dit dekt ook "bron is het doel" en onbekende bron.
3. De gebruiker mag de **bron** beheren → anders `Content.Access.Forbidden`. Beheerrecht (niet alleen leesrecht) omdat de
   bron concepten bevat.
4. Het doel heeft **geen actieve secties** → anders `Content.Copy.TargetNotEmpty` (conflict, 409). Alleen verwijderde
   secties tellen als leeg.

Nieuwe foutcodes: `Content.Copy.InvalidSource`, `Content.Copy.TargetNotEmpty`; titels in `ContentErrors.resx` (nl en en),
in `ContentErrors.All`.

### D5: Tool links en bestanden die niet kunnen worden gekopieerd

- **Tool link**: blijft naar dezelfde `ToolId` wijzen (de tool is voor elke uitvoering beschikbaar, D4 van
  `add-lti-platform`). Bestaat de tool niet meer (`ILtiToolCatalog.ExistsAsync` is onwaar), of is er geen catalogus (de
  module Lti staat uit), dan wordt het item overgeslagen.
- **Bestand**: het bestand op schijf wordt gekopieerd naar `{doelUitvoering}/{nieuwItemId}`. Staat het bronbestand niet
  meer op schijf, dan wordt het item overgeslagen.
- Overgeslagen items tellen mee in `CopyResult(int Sections, int Items, int Skipped)`; de UI meldt ze ("N items could not
  be copied"). Een sectie waarvan alle items zijn overgeslagen wordt wel gekopieerd (lege sectie).

### D6: Alles of niets, en een bekende grens

Zoals `AddFileAsync` (D5 van `add-content`): eerst de entiteiten toevoegen (zodat EF de Id's geeft), dan de bestanden
kopiëren, dan één `SaveChanges`. Mislukt het kopiëren of opslaan, dan worden de al gekopieerde bestanden weer
verwijderd en blijft het doel leeg; de fout bubbelt op (onverwacht, dus geen `Result`). `ContentFileStore.Copy(sourceOffering,
sourceItem, targetOffering, targetItem)` is de nieuwe operatie (`File.Copy`, geen overschrijven); de naam op schijf komt
nooit uit gebruikersinvoer.

De test "alles of niets" laat het kopiëren van het tweede bestand mislukken door het bronbestand exclusief te openen
(`FileShare.None`).

Bekende grens: twee gelijktijdige kopieën naar hetzelfde lege doel kunnen allebei de controle op "leeg" doorstaan en dan
dubbel inhoud geven; de docent verwijdert de dubbele secties. Een uniek-constraint of vergrendeling is het niet waard
voor een handmatige actie van één docent (`ponytail`-keuze); opnieuw bekijken als er automatisch kopiëren komt.

### D7: Beheerpagina

`ManageContent.razor` krijgt een kaart **"Copy content"** met een keuzelijst "Copy from" (de uitvoeringen uit
`SiblingOfferingsAsync`, getoond als "{naam}", dat de periode al bevat) en de knop "Copy content". De kaart staat alleen
op de pagina als er **geen actieve secties** zijn én er andere uitvoeringen zijn; na het kopiëren verdwijnt hij vanzelf
omdat de pagina herlaadt. Zoals de andere acties draait het in een eigen scope. Melding bij succes: "Copied N sections
and M items as drafts." met erachter "K items could not be copied." als K > 0. Fouten via `DcResult`. UI-teksten zijn
Engels en alleen `Dc*`-componenten (huisregel 4). De pagina is al beperkt tot wie de content mag beheren.

Toegankelijkheid: de keuzelijst heeft een zichtbaar label; de melding gebruikt de bestaande `DcAlert`.

### D8: Logging en privacy

Het kopiëren logt niets over de inhoud of gebruiker; hooguit het aantal items. Geen persoonsgegevens in logs.

### D9: Geen API en geen migratie

Er is geen REST-endpoint (de actie is voor docenten in de UI; huisregel 12 is dus niet aan de orde) en geen
databasewijziging. Als een endpoint later nodig is, gebruikt het dezelfde `CopyFromAsync`.

## Risks / Trade-offs

- **Grote bestanden verdubbelen de schijfruimte** → bewust: een kopie is onafhankelijk van de bron. Een gedeeld bestand
  met verwijzingstelling is een later optimalisatiepad.
- **Lange kopieerduur bij veel of grote bestanden** in een interactieve actie → de knop toont een bezig-toestand en blokkeert
  dubbel klikken; achtergrondjob (Hangfire) pas als dat in de praktijk te traag blijkt.
- **De bron-uitvoering van een andere docent**: een docent die de bron niet beheert kan er niet uit kopiëren, ook al is de
  cursus dezelfde (D4); bewust, vanwege concepten.
- **Tool links kunnen naar een inmiddels ongewenste tool wijzen** → de docent ziet de items als concept en beslist bij
  het publiceren.

## Migration Plan

Geen: geen databasewijzigingen en geen configuratie. De nieuwe kaart verschijnt na uitrol vanzelf; uitterugdraaien is het
terugzetten van de code.

## Open Questions

- Willen we later ook content met **publicatiestatus** kopiëren (bv. "Copy and keep published")? Nu bewust nee (D3).
- Moet de bron-keuzelijst later ook uitvoeringen van **andere cursussen** kunnen tonen (bv. een nieuwe cursus die op een
  oude lijkt)? Buiten de scope; apart besluit.
