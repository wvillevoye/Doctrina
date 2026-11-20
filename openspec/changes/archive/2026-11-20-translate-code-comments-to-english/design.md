# Design

## Context

Zie `proposal.md`. Er zijn geen openstaande changes en alleen `main` is actueel, dus een brede diff botst nergens mee.
Het werk is vertaalwerk over ruim 290 bestanden; er is geen code die het kan afleiden, dus het gebeurt met de hand
(met een model), per project, met een mechanische controle achteraf.

## Goals / Non-Goals

**Goals:**
- Alle commentaar in `src/`, `tests/` en `dev-stack/` is Engels; de betekenis blijft hetzelfde.
- Aantoonbaar dat alleen commentaar wijzigt.

**Non-Goals:**
- Geen hernoeming van identifiers, bestanden of testnamen.
- Geen herschrijving van documentatie of OpenSpec-artefacten.
- Geen inhoudelijke verbetering van commentaar (niets toevoegen, niets schrappen) behalve waar vertalen anders
  onleesbaar wordt.

## Decisions

### D1: Vertalen, niet herschrijven

Elke Nederlandse commentaarregel wordt een gelijkwaardige Engelse regel. Commentaar dat achterhaald lijkt, blijft
staan en wordt gemeld in de samenvatting van de apply-stap; opruimen is een aparte afweging. Reden: een diff die alleen
vertaalt is snel te beoordelen, een diff die ook inhoud wijzigt niet.

### D2: Verwijzingen naar specs houden de Nederlandse naam

Een commentaar als `spec ui-components "De stijlgids toont elk component"` verwijst naar een requirementnaam in een
Nederlandstalige spec. Die naam blijft letterlijk staan (tussen aanhalingstekens) zodat zoeken in `openspec/specs/` werkt;
alleen de omringende zin wordt Engels. Alternatief: de naam vertalen. Afgewezen: dan vindt niemand de requirement meer.

### D3: Controle op de diff in plaats van op review van elke regel

Na het vertalen controleert een zoekactie dat elke gewijzigde regel een commentaarregel is:

```
git diff -U0 -- src tests dev-stack ':!*Migrations*' ':!*/vendor/*' | grep -E '^[+-][^+-]' | grep -vE '^[+-]\s*(///|//|@\*|\*|<!--|#|/\*)'
```

Regels met commentaar achter code (`code; // uitleg`) en meerregelige blokken (`@* ... *@`, `/* ... */`) komen als
uitzondering in de uitvoer; die worden met de hand beoordeeld. De uitvoer moet leeg zijn of uitsluitend zulke regels
bevatten. Alternatief: alleen `dotnet test` vertrouwen. Afgewezen: tests vangen een per ongeluk gewijzigde
tekenreeks of attribuutwaarde niet altijd.

### D4: Per project werken, één commit per groep

De bestanden worden in groepen gedaan (zie `tasks.md`), elk met een eigen build- en testronde. Zo blijft een fout klein
en te herleiden, en blijft elke tussenstand groen.

### D5: Controle op resterend Nederlands met een woordenlijst

Na afloop zoekt een lijst veelvoorkomende Nederlandse functiewoorden (`de|het|een|van|voor|niet|met|wordt|zijn|geen|ook|om`)
in commentaarregels. Resterende treffers zijn toegestaan als ze volgens D2 een aangehaalde specnaam zijn of een
eigennaam; de rest wordt vertaald. Dit is een vangnet, geen bewijs: de lijst mist zinnen zonder deze woorden.

## Risks / Trade-offs

- [Vertaalfouten die de betekenis veranderen] → de diff is alleen commentaar en wordt per groep doorgelezen; D1 houdt
  vertalingen letterlijk.
- [Een tekenreeks of attribuut raakt per ongeluk mee] → controle D3 en `dotnet test`.
- [Mergeconflicten met nieuw werk] → de change doet alles in één keer en wordt direct na akkoord uitgevoerd; open
  branches rebasen na de merge.
- [Commentaar in `.razor` kan binnen markup staan] → `@* *@` en `<!-- -->` worden alleen vervangen als hele commentaarblok;
  de markup eromheen blijft staan.

## Open Questions

<!-- geen: scope is met de aanvrager afgestemd (optie 1: commentaar omzetten; identifiers buiten de scope) -->
