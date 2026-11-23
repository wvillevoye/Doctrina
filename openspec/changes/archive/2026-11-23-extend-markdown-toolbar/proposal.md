# Proposal: extend-markdown-toolbar

## Why

De werkbalk van `MarkdownEditor` dekt alleen kop 1-3, vet, cursief, opsomming, link, uitlijning en kleur. Docenten missen
veelgebruikte opmaak: doorhalen, code, genummerde lijst, takenlijst, citaat, codeblok, afbeelding, tabel en een
horizontale lijn. Het voorbeeld in `images/Toolbar.png` (de werkbalk uit UL.MarkdownViewer) laat zien wat verwacht wordt.
Het externe pakket blijft buiten beeld (besluit in `add-markdown-toolbar`); we bouwen de ontbrekende knoppen in de eigen editor.

## What Changes

- Nieuwe werkbalkknoppen in `MarkdownEditor`: "Strikethrough", "Inline code", "Numbered list", "Task list", "Quote",
  "Horizontal rule", "Image" en "Table", plus een keuze "Code block" met taalkeuze (Plain text, C#, SQL, JSON, HTML, Bash).
- De drie kopknoppen worden één keuze "Heading" (Heading 1-3, Normal text) in een native popover, naar het voorbeeld.
- `MarkdownFormatter` krijgt de bijbehorende acties (pure functies, testbaar); `md-editor.js` blijft ongewijzigd.
- `Markdown.ToSafeHtml` zet `~~doorhalen~~` en `- [ ]`-takenlijsten om (Markdig `UseEmphasisExtras` alleen voor
  doorhalen, `UseTaskLists`); HTML blijft uit, afbeeldingen blijven beperkt tot `http(s)`.
- Nieuwe Lucide-iconen in `Doctrina.UI/Icons` (o.a. `strikethrough`, `code`, `list-ordered`, `list-checks`, `quote`,
  `square-code`, `image`, `table`, `minus`, `heading`); CSS voor citaat, code en tabel op `--dc-*`-tokens.

Buiten de scope:
- De upload-knop uit het voorbeeld: Doctrina heeft geen opslag voor afbeeldingen in Markdown. "Image" voegt een URL-sjabloon in.
- Een WYSIWYG-editor, sneltoetsen, syntax highlighting in het voorbeeld, het pakket `UL.MarkdownViewer`.

## Capabilities

### Modified Capabilities
- `markdown-editor`: de werkbalk krijgt de nieuwe knoppen en de kopkeuze.
- `course-content`: de veilige weergave ondersteunt doorhalen en takenlijsten.

## Impact

- `src/Modules/Content`: `MarkdownEditor.razor` (+ css), `MarkdownFormatter`, `Markdown.cs`.
- `src/Doctrina.UI`: nieuwe iconen, CSS-tokens waar nodig.
- Tests in `tests/Modules/Doctrina.Modules.Content.Tests`. Geen nieuwe packages (Markdig levert de extensies), geen database-wijziging.
