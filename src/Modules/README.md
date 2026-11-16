# Modules

Every feature is a module (see `docs/Doctrina-voorstel.md` §3.2 and the house rules in `AGENTS.md`):

```
Modules/<Name>/
├─ Doctrina.Modules.<Name>/             implementation (IModule, Domain, Application, Infrastructure, Api, Components, Events)
└─ Doctrina.Modules.<Name>.Contracts/   public interfaces, DTOs and events for other modules
```

- Own SQL schema (`<name>` in lower case), own DbContext and migrations.
- Other modules only through `.Contracts` or events, never through the implementation (the architecture tests check this).
- Register in `src/Doctrina.Host/KnownModules.cs`, switch on with `Modules:Enabled` in `appsettings.json`.

Step by step: [`docs/ontwikkelen/modules.md`](../../docs/ontwikkelen/modules.md).

| Module | What | Change |
|---|---|---|
| [`Academic`](Academic/) | OOAPI core model (organizations, sessions, courses, offerings, persons, associations), CSV import, admin pages, "My courses" | `add-academic-core` |
| [`Groups`](Groups/) | Group sets and groups in a course offering, manual and random assignment, "My groups" on the offering page | `add-groups` |
| [`Content`](Content/) | Sections with pages (Markdown), files (on disk) and links in a course offering, publish/hide, order, "Content" on the offering page | `add-content` |
| [`Lti`](Lti/) | Doctrina as an LTI 1.3 platform (core): tool registration, signing keys with a public JWKS, launch of a tool link from a course | `add-lti-platform` |
| [`Interop/Doctrina.Modules.Interop.OOAPI`](Interop/) | The core model read-only as OOAPI v5 under `/ooapi/v5` (module name `Ooapi`, permission `Ooapi.Read`); no storage, so no schema or DbContext | `add-ooapi-interop` |
