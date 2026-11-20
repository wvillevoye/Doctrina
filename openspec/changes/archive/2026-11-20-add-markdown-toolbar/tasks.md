# Taken

## 1. Iconen en tokens

- [x] 1.1 Voeg de Lucide-iconen `heading-1`, `heading-2`, `heading-3`, `bold`, `italic`, `list`, `link` en `palette` toe aan `src/Doctrina.UI/Icons` (zelfde versie als `VERSION`, ISC); controleer dat `DcIconSet.Names` ze bevat en dat de stijlgids de iconen toont
- [x] 1.2 Voeg `--dc-md-accent`, `--dc-md-success`, `--dc-md-warning`, `--dc-md-danger` en `--dc-md-muted` toe aan het lichte en het donkere thema in `_tokens.scss` (verwijzingen naar de bestaande tokens, design D3); controleer dat `dotnet build` slaagt
- [x] 1.3 Breid `KleurcontrastVoldoetAanWcag22AaInBeideThemas` uit met de vijf paren per thema (op `--dc-surface`, minstens 4,5:1); pas de tokenwaarden aan tot de test slaagt

## 2. Weergave van kleur

- [x] 2.1 Voeg in `Application/Markdown.cs` de nabewerking uit design D2 toe: een `ColorInline` met renderer die `<span class="md-color md-color--naam">` schrijft voor de vijf vaste namen; onbekende en ongesloten markeringen blijven tekst, code blijft onaangeroerd
- [x] 2.2 Voeg `MarkdownView.razor.css` toe met `::deep .md-color--*` op de `--dc-md-*`-tokens; controleer dat de architectuurtest `LiteralColors` slaagt

## 3. Opmaaklogica

- [x] 3.1 Schrijf `Application/MarkdownFormatter.cs` met `MarkdownAction` (`Heading1..3`, `Bold`, `Italic`, `BulletedList`, `Link`, `Color(naam)`), `EditorState(Text, Start, End)` en de pure functie `Apply` volgens design D4
- [x] 3.2 Schrijf `wwwroot/md-editor.js` (twee functies: `getState`, `setState` met focus en `input`/`change`-events, design D1); controleer het pad `_content/Doctrina.Modules.Content/md-editor.js`

## 4. Component en pagina's

- [x] 4.1 Schrijf `Components/MarkdownEditor.razor` en `.razor.css`: werkbalk (`role="toolbar"`, naam "Formatting"), acht knoppen met icoon en `Label`, kleurkeuze met het native popover-attribuut, `DcTextField` met `Rows`, `Value`/`ValueChanged`/`ValueExpression`, importeert het JS-module via `IJSRuntime`
- [x] 4.2 Vervang in `EditContentPage`, `EditIntroductionPage` en bij "Starting text" in `ManageTemplatesPage` het `DcTextField Rows` door `MarkdownEditor`; controleer dat "Preview" en de bestaande tests slagen

## 5. Tests

- [x] 5.1 Schrijf `DeWerkbalkVoegtMarkdownSyntaxIn` (unit, op `MarkdownFormatter`) met de scenario's `KopMakenVanEenRegel`, `KopVervangtEenBestaandeKop`, `VetOmDeSelectie`, `PlaatshouderZonderSelectie`, `OpsommingOverMeerdereRegels` en `LinkMaken`
- [x] 5.2 Schrijf `DeKleurkeuzeGebruiktEenVasteSetThemaKleuren` (bUnit op `MarkdownEditor`, JS-interop als testdubbel) met `KleurKiezen` en `KleurkeuzeZonderEigenKleurcode`, en `DeWerkbalkVoegtMarkdownSyntaxIn.TekstDieAlGetypteWasGaatNietVerloren` (de testdubbel geeft de staat uit de browser terug)
- [x] 5.3 Schrijf `DeWerkbalkIsToegankelijkEnHoudtDeFocusInHetTekstveld` met `NamenEnRol` en `FocusBlijftInHetVeld` (de testdubbel controleert dat `setState` de focus zet)
- [x] 5.4 Schrijf `PaginasVoorMarkdownGebruikenDezelfdeEditor` met `EditorOpDeContentpagina` en `EditorBijDeIntroductieEnSjablonen`
- [x] 5.5 Schrijf in `AccessAndMarkdownTests` (of een nieuw bestand) de scenario's van `PaginasWordenInMarkdownGeschrevenEnVeiligWeergegeven`: `KleurWordtWeergegeven`, `KleurOmOpgemaakteTekst`, `OnbekendeOfOngeslotenKleurBlijftTekst`, `KleurInCodeBlijftTekst`; controleer dat de bestaande veiligheidstests nog slagen
- [x] 5.6 Schrijf `TekstkleurenHebbenVoldoendeContrastInBeideThemas.ContrastVanDeTekstkleuren` (kan samenvallen met 1.3; zorg dat de naam naar de requirement verwijst)

## 5b. Uitlijning en UM-kleuren

- [x] 5b.1 Iconen `align-left`, `align-center`, `align-right` (Lucide, zelfde versie); tokens `--dc-md-blue` en `--dc-md-lightblue` in beide thema's met contrasttest
- [x] 5b.2 `Markdown.cs`: namen `blue`, `lightblue` en `left`/`center`/`right` (klasse `md-align md-align--naam`); CSS in `MarkdownView.razor.css`
- [x] 5b.3 `MarkdownFormatter` en `MarkdownEditor`: drie uitlijnknoppen, palet met zeven opties en UM-labels
- [x] 5b.4 Tests voor de scenario's `Tekst centreren`, `Uitlijning wordt weergegeven`, het palet met zeven opties en de contrasten

## 5c. Verschuifbaar voorbeeld

- [x] 5c.1 `PreviewWindow` met greep, sluitknop en `makeDraggable` in `md-editor.js`; gebruik het op `EditContentPage` en `EditIntroductionPage`
- [x] 5c.2 Test `HetVoorbeeldIsEenVerschuifbaarVenster.VoorbeeldOpenenEnSluiten`

## 6. Afronding

- [x] 6.1 Voeg een korte alinea (Engels) toe aan `docs/ontwikkelen/vormgeving.md` over de `{naam}…{/naam}`-kleuren (vaste set, grenzen van containers) en de `MarkdownEditor`
- [ ] 6.2 Open de pagina's in de browser in beide thema's: controleer de knoppen met muis en toetsenbord, de kleurkeuze, de focus na een klik en de kleuren in voorbeeld en weergave
- [x] 6.3 Voer `openspec validate add-markdown-toolbar --strict` uit en draai de volledige testset
