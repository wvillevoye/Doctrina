# Design

## Context

Zie `proposal.md` voor aanleiding en scope. Waar deze change op voortbouwt:

- **Academic**: `IAcademicDirectory.MembersAsync(offeringId)` geeft de actieve inschrijvingen van actieve personen met
  `PersonId`, `ExternalId`, naam en `OfferingRole` (`Student`, `Lecturer`, `TeachingAssistant`, `Coordinator`), één rij per
  inschrijving. Naam, voornaam, achternaam en e-mail komen apart uit `FindPersonAsync(personId)` (één query per persoon).
- **Launch**: `LaunchService.SubjectAsync` maakt het `sub`: de `PersonId` ("N") van de ingelogde persoon. De rollen
  worden in `IdToken` afgebeeld op LIS-URI's (`Learner`, `Instructor`, `Instructor#TeachingAssistant`); naam en e-mail gaan
  alleen mee als `LtiTool.SendName`/`SendEmail` aan staan.
- **`add-lti-ags` (8b, nog niet gebouwd)** levert het tokenendpoint `POST /lti/token` (`client_assertion`), access tokens
  als JWT met scopes, de validatiehulp voor service-endpoints en de aanpak "endpoints onder `/lti/…` op de root met eigen
  tokencontrole" (D1–D3 daar), én `IContentDirectory.ToolLinksOfToolAsync` (welke tool links een tool in een uitvoering
  heeft). Deze change gebruikt alles daarvan en bouwt het niet opnieuw.

## Goals / Non-Goals

**Goals:**
- Een standaard NRPS 2.0-tool haalt de deelnemers met rollen op, gepagineerd.
- Dezelfde `user_id` als `sub` in de launch en als `userId` in AGS, zodat een tool alles aan elkaar kan koppelen.
- Niet meer persoonsgegevens delen dan bij een launch.

**Non-Goals:** zie het proposal (meldingen, `differences`, `message`, groepen, niet-ingeschreven gebruikers).

## Decisions

### D1: Volgorde en afhankelijkheid van 8b

Deze change bouwt voort op `lti-service-auth` en `lti-grade-services` uit `add-lti-ags`, die pas na het archiveren van 8b
als hoofdspec bestaan. Daarom bevatten de delta-specs hier alleen ADDED-requirements in nieuwe of bestaande
capabilities; niets wijzigt een requirement van 8b. Twee aanpassingen aan 8b-gedrag vallen daarmee in de taken, niet in de
specs: (a) het tokenendpoint kent de NRPS-scope toe aan tools met `MembershipServices` (de requirement "Scopes worden
beperkt tot wat de tool mag" van 8b noemt alleen AGS-scopes; de nieuwe scenarios staan hier in `lti-memberships`), en
(b) de regel "Public keys URL zonder deep linking URL" uit `lti-tool-registration` laat nu `GradeServices` toe en moet ook
`MembershipServices` toelaten; die tekstwijziging doen we met `/opsx:update` zodra 8b is gearchiveerd, vóór het bouwen
van taak 1.1.

### D2: Endpoint `GET /lti/nrps/{context}/memberships`

Op de root (`MapBrowserEndpoints`), anoniem voor de host, met de tokencontrole van 8b (scope
`…/lti-nrps/scope/contextmembership.readonly`). Toegang volgt D4 van 8b: alleen een uitvoering waarin de tool een tool link
heeft; anders 404 (geen lek van het bestaan).

### D3: Eén rij per persoon

Het antwoord toont een persoon één keer, met de unie van haar rollen (afgebeeld zoals in `IdToken`; die afbeelding wordt
een gedeelde methode in plaats van een kopie). `user_id` is de `PersonId` ("N", gelijk aan `sub`). Beheerders zonder
inschrijving komen er niet in voor (ze zijn geen lid).

### D4: Gepagineerd leescontract in Academic

Per persoon `FindPersonAsync` aanroepen kost bij 500 deelnemers 500 queries. Daarom krijgt `IAcademicDirectory` een
methode `MemberPageAsync(offeringId, roleFilter, skip, take)` die per pagina `OfferingMemberDetail` teruggeeft (persoon-id,
rollen, naam, voornaam, achternaam, e-mail) met één query, stabiel op persoon-id gesorteerd. De module Lti haalt de
gegevens die de tool niet mag zien niet op: de namen worden hoe dan ook gelezen, maar niet doorgegeven zonder instelling
(D5). `MembersAsync` blijft ongewijzigd (andere modules gebruiken het).

*Alternatief:* `MembersAsync` uitbreiden. Verworpen: het zou bestaande aanroepers meer gegevens laten laden.

### D5: Privacy

Naam en e-mail volgen exact `SendName`/`SendEmail` van de tool, dus nooit meer dan in een launch. `role`, `limit` en
`rlid` komen uit de querystring en worden gevalideerd (limit 1–500, `role` een bekende waarde of URI, `rlid` een Guid). Er
wordt niets van de inhoud gelogd (D7).

### D6: Paginering

`limit` standaard 50, hoogstens 500; de volgende pagina via een `Link`-header met `rel="next"` (een ondoorzichtige
paginasleutel op de laatste persoon-id, geen offset, zodat wijzigingen tijdens het doorlopen geen dubbelen geven). `rlid`
wordt gecontroleerd (D2) maar verandert de lijst niet: Doctrina kent geen per-item-zichtbaarheid per deelnemer die een
tool zou moeten volgen (een studentenlijst is voor alle items van de uitvoering gelijk).

### D7: Geen persoonsgegevens in logs

Niets van `user_id`, namen of e-mailadressen wordt gelogd; getest met `CapturingLoggerProvider` op `Trace`.

### D8: Launch-claim

`IdToken.Payload` voegt voor tools met `MembershipServices` de claim `…/lti-nrps/claim/namesroleservice` toe met
`context_memberships_url` = `{issuer}/lti/nrps/{offering}/memberships` en `service_versions` `["2.0"]`.

## Risks / Trade-offs

- **Afhankelijk van een change die nog niet is gebouwd**: elke wijziging in het ontwerp van 8b (tokenvorm, scopes, URL's)
  raakt deze change. Beperking: 8c begint pas na 8b (D1) en de taken verwijzen naar de 8b-hulpen bij naam.
- **Alle deelnemers voor alle items**: `rlid` vernauwt niets. Voor tools die alleen "wie mag dit item zien" willen is dat te
  ruim, maar nooit ruimer dan de uitvoering; de beheerder kiest de tool.
- **Persoonsgegevens verlaten Doctrina in bulk**: één aanroep levert tot 500 personen. Alleen tools die de beheerder
  bewust "Membership services" geeft (standaard uit) en alleen binnen uitvoeringen waar ze al een tool link hebben.
- **Geen wijzigingsmeldingen**: tools moeten opnieuw opvragen om wijzigingen te zien.

## Open vragen

- Moet een student-rol zonder naam toch iets als "Student 1" krijgen voor tools die dat willen? Nu niet (de standaard laat
  naam en e-mail weg).
