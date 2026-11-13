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
