# Voorstel

## Why

Een cursus wordt elk jaar opnieuw gegeven. Zonder hulp bouwt een docent de contentstructuur (secties, pagina's, bestanden,
links en tool links) voor elke nieuwe uitvoering opnieuw op. Deze change is rij 9 van de roadmap (§8 van
`docs/Doctrina-voorstel.md`, `add-course-templates`; §6.3): een nieuwe uitvoering start met de inhoud van een eerdere
uitvoering van dezelfde cursus. Rij 3, 7 en 8 (Academic, Content, LTI) zijn af, dus de afhankelijkheden zijn gedekt.

Keuzes van de gebruiker (2026-11-14), op de open vraag in §6.3 en §10 van het voorstel:
- **Cursussjablonen alleen**: herbruikbare contentblok-templates komen niet in deze change.
- **De template is een bestaande uitvoering** van dezelfde cursus (bv. die van vorig jaar), geen aparte template-inhoud
  per `Course`. Dat wijkt af van "een Course dient als template" in §6.3; het besluit staat in het design (D1).
- **Handmatig toepassen** op een lege uitvoering, niet automatisch bij het aanmaken van een uitvoering.

## What Changes

- **Content kopiëren naar een lege uitvoering**: op de beheerpagina van de content van een uitvoering zonder secties
  staat een kaart "Copy content" met een keuzelijst van de andere uitvoeringen van dezelfde cursus. Eén klik kopieert
  alle secties en items (pagina's, bestanden, links en tool links) naar deze uitvoering, **als concept**: studenten
  zien niets totdat de docent publiceert.
- **Wie mag het**: wie de content van de nieuwe uitvoering mag beheren **en** die van de bron (docent of coördinator van
  beide, of `Content.Manage`); de bron kan concepten bevatten.
- **Wat niet meegaat**: verwijderde items, publicatiestatus, deelnemers en groepen. Een tool link waarvan de tool niet
  meer bestaat, en een bestand dat niet meer op schijf staat, worden overgeslagen en geteld in de melding.
- **Contract in Academic**: `IAcademicDirectory.SiblingOfferingsAsync` geeft de andere actieve uitvoeringen van dezelfde
  cursus (Content mag Academic niet rechtstreeks bevragen).
- Documentatie: `docs/ontwikkelen/starten.md` (een uitvoering vullen uit een eerdere) en §6.3 van
  `docs/Doctrina-voorstel.md` (open vraag beantwoord voor de eerste stap).

## Buiten de scope

- **Contentblok-templates** (herbruikbare blokken los van een uitvoering) en pagina-/layouttemplates.
- **Een uitvoering aanmaken** (die komt uit de SIS, CSV-import of API) en **automatisch kopiëren** bij een nieuwe
  uitvoering, dus ook geen domein-event uit Academic.
- Kopiëren naar een uitvoering die al content heeft (samenvoegen), kopiëren tussen uitvoeringen van verschillende
  cursussen, en een REST-endpoint of bulkkopie.
- Meekopiëren van groepen (Groups), deelnemers of LTI-registraties; verwijzingen naar tools blijven dezelfde tool.
- Een undo: een foute kopie verwijdert de docent met de bestaande verwijderknoppen.

## Capabilities

### New Capabilities
- Geen.

### Modified Capabilities
- `course-content`: een nieuwe eis "Content kopiëren naar een lege uitvoering".

## Impact

- `Doctrina.Modules.Academic.Contracts`: `IAcademicDirectory.SiblingOfferingsAsync`; implementatie in `AcademicDirectory`;
  `FakeDirectory` in TestKit.
- Module Content: `ContentService.CopyFromAsync`, `ContentFileStore.Copy`, foutcodes `Content.Copy.*`, de kaart op
  `ManageContent.razor`, resx-titels (nl/en). Geen nieuwe tabellen of migraties.
- Tests: Content-tests (LocalDB), een Academic-test voor `SiblingOfferingsAsync`, een integratietest op het web.
- Documentatie: `docs/ontwikkelen/starten.md`, `docs/Doctrina-voorstel.md` §6.3.
- Geen nieuwe packages, geen configuratie, geen databasewijzigingen.
