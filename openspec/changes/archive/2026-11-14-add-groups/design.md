# Design

## Context

Zie `proposal.md` voor aanleiding en scope. Waar deze change op voortbouwt (uit `add-academic-core` en eerder):

- Module Academic met `CourseOffering`, `Person`, `Association` (rollen `student`, `lecturer`, `teaching-assistant`,
  `coordinator`) en `CurrentPerson` (login → persoon via `personId`, daarna issuer + `oid`). `Doctrina.Modules.Academic.Contracts`
  bestaat maar is leeg.
- Modules mogen elkaar alleen via `.Contracts` of events kennen; de architectuurtests bewaken dat. `IModule.DependsOn`
  bepaalt de laadvolgorde.
- `IEventBus` publiceert domeinevents ná een geslaagde `SaveChanges`. De `DomainEventsInterceptor` haalt events op van
  elke gevolgde `Entity`, ongeacht haar status.
- EF Core genereert `Guid`-sleutels sequentieel aan de clientkant op het moment van `Add`, dus vóór het opslaan.
- Paginarechten gaan via `[RequirePermission]`. Rechten die van de uitvoering afhangen bestaan nog niet.

## Goals / Non-Goals

**Goals:**
- Een tweede module die laat zien hoe modules samenwerken: een contract van Academic, een event van Groups en een
  pagina waarop meerdere modules iets tonen.
- Rechten per uitvoering voor groepen, zonder een algemeen rechtenmodel per uitvoering te bouwen.

**Non-Goals:**
- Een algemeen rechtenmodel per uitvoering voor andere modules. `CanManageGroups` is specifiek voor Groups (D5); het
  generaliseren wacht tot een tweede module het nodig heeft.
- Zelf inschrijven en zichtbaarheid per rol (D7).

## Decisions

### D1: Academic.Contracts: een smalle directory en secties voor de uitvoeringspagina

`Doctrina.Modules.Academic.Contracts` krijgt:

- `IAcademicDirectory` met `FindOfferingAsync(offeringId)` → `Result<OfferingInfo>` (alleen actieve uitvoeringen,
  anders `Academic.CourseOffering.NotFound`), `CurrentPersonIdAsync(user)` (via de bestaande `CurrentPerson`, met de
  koppeling bij het eerste gebruik) en `MembersAsync(offeringId)` → actieve inschrijvingen van actieve personen, met
  `PersonId`, `ExternalId`, naam en rol (`OfferingRole`).
- `OfferingPageSection(Type Component, int Order)` en een extensie `services.AddOfferingPageSection<TComponent>(order)`.
  Het component krijgt `[Parameter] Guid OfferingId`.

Alternatief: Groups leest de tabellen van Academic direct (zelfde database). Afgewezen: dat breekt de modulegrens die
de architectuurtests bewaken, en een schemawijziging in Academic zou Groups stilletjes breken.

### D2: De uitvoeringspagina hoort bij Academic en toont secties met `DynamicComponent`

`/courses/{offering:guid}` staat in Academic (`CoursePage`). De pagina toont de gegevens uit Academic en daarna elke
geregistreerde `OfferingPageSection` in volgorde, met `<DynamicComponent Type=… Parameters=…>`. Secties van een
uitgeschakelde module zijn er niet: hun registratie gebeurt in `ConfigureServices`, en dat draait alleen voor
ingeschakelde modules. Toegang: een actieve inschrijving in de uitvoering of `Academic.Read`. Anders volgt een redirect
naar `/access-denied` (403), net als bij de cookie-handler. Een onbekende of te verwijderen uitvoering geeft
`NavigationManager.NotFound()` (404).

Alternatief: Groups maakt een eigen pagina `/courses/{offering}/my-groups`. Afgewezen: elke volgende module (content,
LTI) zou dan een losse pagina maken, en de student heeft geen plek die de uitvoering als geheel laat zien.

### D3: Datamodel in schema `groups`

| Entiteit | Velden | Sleutels en indexen |
|---|---|---|
| `GroupSet` (uitwisselbaar) | `OfferingId` (Guid, geen FK naar een ander schema), `Name`, `VisibleToStudents`, `MaxGroupSize?` | index op `OfferingId` |
| `Group` (uitwisselbaar) | `GroupSetId` (FK), `Name` | |
| `GroupMember` | `GroupSetId`, `GroupId` (FK, cascade), `PersonId` (Guid, geen FK) | uniek op (`GroupSetId`, `PersonId`): hoogstens één groep per set |

Groepen zijn LMS-data (§4.4): `Source` = `Doctrina`, en `ExternalId` = het eigen `Id` in `"D"`-notatie. EF vult dat
`Id` al bij `Add`, dus beide liggen vast vóór het opslaan. Er zijn geen willekeurige GUID's in de code (huisregel 5).
`GroupSetId` staat ook op het lid, zodat de database de regel "één groep per set" afdwingt.

Verwijderen van een GroupSet of groep zet de status op `ToBeDeleted`, zoals het voorstel voor `DELETE` vraagt (§6.6).
De leden worden wel echt verwijderd (met events), want een student in een verwijderde groep is niet meer ingedeeld.
Pagina's tonen alleen actieve sets en groepen.

### D4: Eén service voor alle wijzigingen, events op de groep

`GroupService` (in de module) doet aanmaken, hernoemen, verwijderen, toevoegen, verplaatsen, verwijderen van een lid en
willekeurig verdelen. Elke methode geeft een `Result` met foutcodes `Groups.*`, bijvoorbeeld `Groups.Group.Full`,
`Groups.Member.NotAStudent` of `Groups.GroupSet.NotFound`. De events `GroupMembershipChanged(OfferingId, GroupSetId,
GroupId, PersonId, Change)` worden geraised op de `Group`, het aggregaat. Zo worden ze ook afgeleverd als het lid zelf
wordt verwijderd, want de interceptor kijkt naar elke gevolgde entiteit met events. Bij verplaatsen ontstaan twee
events: eerst "removed" op de oude groep, dan "added" op de nieuwe. Het event staat in `Doctrina.Modules.Groups.Contracts`.

### D5: Wie mag beheren: één check in Groups

`GroupsAccess.CanManageAsync(user, offeringId)` is waar als de gebruiker de permissie `Groups.Manage` heeft, of als zijn
persoon (via `IAcademicDirectory.CurrentPersonIdAsync`) in `MembersAsync(offeringId)` voorkomt met rol `Lecturer` of
`Coordinator`. `teaching-assistant` mag niet beheren; dat is de rol die het dichtst bij een student staat, en uitbreiden
is één regel. De beheerpagina en de "Manage groups"-link gebruiken dezelfde check. `Groups.Manage` heeft geen eigen
rolkoppeling: `admin` heeft `*`.

### D6: Willekeurig verdelen

Kandidaten zijn de studenten uit `MembersAsync` die in de set nog geen groep hebben. Ze worden geschud met
`Random.Shared.Shuffle`. Het aantal groepen is het gevraagde aantal N, of bij "groepen van X" `ceil(aantal / X)`.
Daarna worden de kandidaten om de beurt verdeeld, zodat de groottes hoogstens één verschillen. Nieuwe groepen heten
"Group k", doorgenummerd na de hoogste bestaande "Group n" in de set. Heeft de set een maximale groepsgrootte en zou een
nieuwe groep die overschrijden, dan wordt het geweigerd met `Groups.Group.Full`, voordat er iets is opgeslagen. Alles
gebeurt in één `SaveChanges`.

Alternatief: ook bestaande groepen aanvullen. Afgewezen voor nu: dat vraagt keuzes over volgorde en eerlijkheid, die
beter samen met zelf inschrijven worden gemaakt.

### D7: Afwijkingen van het voorstel (§6.2)

- **Zelf inschrijven** valt buiten deze change, op keuze van de gebruiker. Handmatig en willekeurig dekken de meeste
  vakken.
- **"Zichtbaar voor rollen"** wordt één keuze "zichtbaar voor studenten". Docenten van de uitvoering en beheerders zien
  altijd alles; voor `teaching-assistant` is er nog geen apart beheer (D5). Fijner per rol kan later zonder het
  datamodel te breken.
- **Alleen studenten zijn groepslid.** Begeleiders per groep horen bij een latere change.

### D8: Pagina's

- `/courses/{offering}` (Academic, statisch): kop met cursus en uitvoering, `dl.dc-details` met periode en eigen rol,
  daarna de secties.
- Sectie "My groups" (Groups, statisch): per zichtbare set de eigen groep met de namen van de leden, als `DcCard`.
  Voor beheerders de knop "Manage groups". Zonder groepen: "You are not in any groups yet."
- `/courses/{offering}/groups` (Groups, interactief zoals de importpagina): een `DcCard` per set met de groepen en hun
  leden, formulieren om sets en groepen te maken en te hernoemen, een lijst "Not in a group" met per student een
  keuzelijst om in te delen, en een formulier "Distribute randomly" (aantal groepen of groepsgrootte). Verwijderen gaat
  via `DcModal` ter bevestiging.

## Risks / Trade-offs

- [Een student wordt uitgeschreven (inschrijving `ToBeDeleted`) maar blijft groepslid] → pagina's tonen alleen leden
  die nog in `MembersAsync` staan. Het lid opruimen hoort bij een latere change; het event voor dat geval ook.
- [`OfferingId`/`PersonId` zonder foreign key naar Academic kunnen naar niets wijzen] → Groups controleert bij elke
  wijziging via het contract. Een uitvoering die `ToBeDeleted` wordt, verbergt haar pagina's (404), zodat haar sets
  onbereikbaar zijn.
- [`MembersAsync` laadt alle leden van een uitvoering per verzoek] → bij honderden studenten prima. Bij grote
  uitvoeringen later filteren in het contract.
- [`DynamicComponent` maakt fouten in een sectie pas zichtbaar tijdens het renderen] → een integratietest rendert de
  pagina met de sectie van Groups.

## Migration Plan

Nieuw schema `groups` met een eerste migratie. Aanzetten in `appsettings.Development.json`
(`Modules:Enabled: [Academic, Groups]`). Terugdraaien: module uitzetten; de sectie verdwijnt van de uitvoeringspagina.
Archiveren pas na `add-academic-core` (en de changes daarvóór).
