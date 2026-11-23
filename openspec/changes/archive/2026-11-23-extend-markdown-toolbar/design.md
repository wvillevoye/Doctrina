# Design: extend-markdown-toolbar

## Context
`MarkdownFormatter.Apply` (pure, `EditorState` in en uit) en `MarkdownEditor` bestaan al; kleur en uitlijning gebruiken
een popover. We volgen dat patroon.

## Besluiten

**D1 — Alles in `MarkdownFormatter`, JS blijft gelijk.** Nieuwe `MarkdownActionKind`-waarden; geen nieuwe script-logica.
Onderbouwing: eerder besluit D1 van `add-markdown-toolbar`, en pure functies zijn goed testbaar.

**D2 — Regelacties hergebruiken `EditLines`.** Genummerde lijst (`1. `, `2. `, …), takenlijst (`- [ ] `), citaat (`> `):
zelfde aan/uit-gedrag als de opsomming. Een lijstsoort vervangt een andere (`- ` wordt `1. `) in plaats van te stapelen.

**D3 — Omwikkelen met `Wrap`.** Doorhalen `~~…~~`, inline code met backticks.

**D4 — Invoegsjablonen.** Codeblok: een openingsregel met drie backticks en de taal, de selectie, en een sluitregel, elk op
een eigen regel. Afbeelding: `![alt](https://)` met adres geselecteerd (zoals Link). Tabel: 2×2-pipetabel met koppen.
Horizontale lijn: `---` met lege regel ervoor en erna. Zonder selectie wordt een plaatshouder geselecteerd.

**D5 — Kopkeuze en taalkeuze als popover, geen `select`.** Zelfde mechaniek als de kleurkeuze (native `popover`, geen JS);
"Normal text" haalt het kopprefix weg. Taalnamen zijn een vaste lijst, geen vrije invoer.

**D6 — Weergave via Markdig-extensies, HTML blijft uit.** `UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)` en
`UseTaskLists()`. Takenlijst-selectievakjes worden door Markdig als `disabled` weergegeven; geen invoer via de pagina.
`DisableHtml()` en de URL-controle op afbeeldingen blijven ongewijzigd.

**D7 — Geen upload.** Er is geen bestandsopslag voor Markdown-afbeeldingen; een upload vraagt een eigen change met
opslag en toegangsregels.

**D8 — Iconen en stijl.** Lucide-iconen (zelfde versie als `VERSION`), stijl op `--dc-*`-tokens, geen ruwe Bootstrap-classes.

## Risico's
- De werkbalk wordt lang: laten omslaan (`flex-wrap`) en groeperen met scheidingslijnen; `role="toolbar"` blijft.
