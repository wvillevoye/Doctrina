# Taken

## 1. Iconen en stijl

- [x] 1.1 Voeg de Lucide-iconen `heading`, `strikethrough`, `code`, `list-ordered`, `list-checks`, `quote`, `square-code`, `image`, `table` en `minus` toe aan `src/Doctrina.UI/Icons` (zelfde versie als `VERSION`); controleer `DcIconSet.Names` en de stijlgids
- [x] 1.2 Voeg stijl toe voor citaat, code, codeblok en tabel in de weergave op `--dc-*`-tokens; controleer dat de architectuurtest `LiteralColors` slaagt

## 2. Weergave

- [x] 2.1 `Markdown.cs`: `UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)` en `UseTaskLists()`; controleer dat de bestaande veiligheidstests slagen

## 3. Opmaaklogica

- [x] 3.1 `MarkdownFormatter`: acties `Strikethrough`, `InlineCode`, `NumberedList`, `TaskList`, `Quote`, `CodeBlock(taal)`, `Image`, `Table`, `HorizontalRule`, `NormalText` volgens design D2-D4
- [x] 3.2 `MarkdownEditor.razor`: kopkeuze en taalkeuze als popover (D5), nieuwe knoppen met icoon en `Label`, groepering met scheiding; geen uploadknop

## 4. Tests

- [x] 4.1 `DeWerkbalkVoegtMarkdownSyntaxIn` (unit): `NormaleTekstHaaltDeKopWeg`, `Doorhalen`, `CodeInDeRegel`, `GenummerdeLijst`, `LijstsoortWisselen`, `Takenlijst`, `Citaat`, `CodeblokMetTaal`, `AfbeeldingInvoegen`, `TabelInvoegen`, `HorizontaleLijn`; de bestaande kopscenario's lopen nu via de kopkeuze
- [x] 4.2 bUnit op `MarkdownEditor`: `DeWerkbalkVoegtMarkdownSyntaxIn.GeenUploadknop`; pas `NamenEnRol` en de kleurtests aan op de nieuwe knoppen
- [x] 4.3 `PaginasKunnenDoorhalenEnTakenlijstenGebruiken`: `DoorhalenWordtWeergegeven`, `TakenlijstWordtWeergegeven`, `HtmlBlijftUitNaastDeNieuweOpmaak`
- [x] 4.4 Pas `docs/ontwikkelen/vormgeving.md` kort aan (Engels) en voer `dotnet build` en de testsuite uit
