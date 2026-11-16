# Proposal

## Why

Een LTI-tool die een groep studenten bedient (een quiz, een discussietool) moet weten wie er in de cursus zit en met welke
rol, zonder dat elke student eerst moet starten. LTI Names and Role Provisioning Services (NRPS) laat een tool de
deelnemerslijst van een uitvoering ophalen. Dit is rij 8c van de fasering (§8 van `docs/Doctrina-voorstel.md`; §6.5 noemt
NRPS "deelnemerslijst"). Doctrina bewaart de deelnemers al (Academic: inschrijvingen met rol), dus het is een
alleen-lezen weergave; de beveiligde aanroep van de tool komt uit `add-lti-ags` (8b).

## What Changes

- Een tool met **ledenservices** aan kan met een access token (scope
  `https://purl.imsglobal.org/spec/lti-nrps/scope/contextmembership.readonly`) onder `/lti/nrps/{context}/memberships` de
  deelnemers van een uitvoering ophalen: `user_id`, `status`, `roles` en, alleen als de tool dat mag, naam en e-mail.
- De `id_token` van een launch bevat voor zo'n tool de NRPS-claim met de URL van die lijst.
- De toolregistratie krijgt een instelling "Membership services"; een `Public keys URL` is dan verplicht.
- Het contract van Academic krijgt een gepagineerde leesmethode voor deelnemers met persoonsgegevens, zodat een lijst
  niet per persoon een query kost.
- Documentatie (Engels) en de testtool uitbreiden voor de handmatige controle.

**Afhankelijk van `add-lti-ags`** (8b): het tokenendpoint, de toegangstokens en de validatie van service-endpoints worden daar
gebouwd en hier hergebruikt. Deze change begint pas na het afronden en archiveren van 8b; zie design D1.

**Buiten de scope:**
- De optionele melding bij ledenwijzigingen (Context Memberships Changed), het `differences`-adres en de `message`-lijst
  per deelnemer met resource-link-specifieke gegevens.
- Groepen (`GroupMembershipChanged` uit §3.5): NRPS 2.0 kent geen groepen in de kernlijst.
- Deelnemers die niet in Academic staan (bv. een beheerder zonder inschrijving).
- Per-scope instellingen per tool; één scope.
- Rolwijzigingen sturen naar de tool; de tool vraagt zelf op.

## Capabilities

### New Capabilities
- `lti-memberships`: een tool haalt de deelnemers van een uitvoering op via NRPS 2.0: lijst, rollen, paginering,
  filters en privacyregels.

### Modified Capabilities
- `lti-tool-registration`: instelling "Membership services" per tool, met een verplichte `Public keys URL`.
- `lti-launch`: de `id_token` bevat de NRPS-claim voor tools met ledenservices.

## Impact

- Code: module `Lti` (`MembershipService`, endpoint `/lti/nrps/{context}/memberships`, uitbreiding van de scopes in het
  tokenendpoint, `IdToken`), `Doctrina.Modules.Academic.Contracts` + implementatie (gepagineerde deelnemers met
  personen), `Doctrina.Modules.Content.Contracts` voor de controle dat de tool in de uitvoering een tool link heeft
  (komt uit 8b).
- Data: alleen een kolom `MembershipServices` op `LtiTool` (migratie `AddMembershipServices`); geen nieuwe tabellen.
- Geen nieuwe packages.
- Documentatie: `docs/ontwikkelen/starten.md` (Engels), `docs/Doctrina-voorstel.md` §6.5/§8.
