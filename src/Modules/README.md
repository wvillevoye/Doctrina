# Modules

Elke feature is een module (zie `docs/Doctrina-voorstel.md` §3.2 en de huisregels in `AGENTS.md`):

```
Modules/<Naam>/
├─ Doctrina.Modules.<Naam>/             implementatie (IModule, Domain, Application, Infrastructure, Api, Components, Events)
└─ Doctrina.Modules.<Naam>.Contracts/   publieke interfaces, DTO's en events voor andere modules
```

- Eigen SQL-schema (`<naam>` in kleine letters), eigen DbContext en migraties.
- Andere modules alleen via `.Contracts` of events — nooit via de implementatie (architectuurtests bewaken dit).
- Bekendmaken in `src/Doctrina.Host/KnownModules.cs`, aanzetten via `Modules:Enabled` in `appsettings.json`.

Stap voor stap: [`docs/ontwikkelen/modules.md`](../../docs/ontwikkelen/modules.md).
Er zijn nog geen productmodules; de eerste komt met change `add-academic-core`.
