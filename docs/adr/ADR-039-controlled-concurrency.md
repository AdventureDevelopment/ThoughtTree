# ADR-039: No uncontrolled concurrency or timers

id: ADR-039 · status: Accepted · kind: constraint

applies_to: `src/**`

- **MUST NOT** use `Thread`, `Task.Run`, `Parallel`, `ThreadPool`, `System.Threading.Timer`, `System.Timers.Timer` or `Task.Delay` without a `TimeProvider` in client logic, Domain or the server core
- **MUST** do all waiting and timing through `TimeProvider` (`Task.Delay(TimeSpan, TimeProvider)`, `TimeProvider.CreateTimer`)
- **MUST** list the forbidden APIs in the banned API analyzer for Domain, Client.App and Server.Core; in the Client and Server projects, where hosting code shares the project, architecture tests enforce them per namespace
- **MAY** use them in the thin hosting layers (ASP.NET Core endpoints, browser interop) where the simulation does not run

verify:

```bash
dotnet build -warnaserror   # banned APIs are compile errors
dotnet test --filter "ADR=ADR-039"   # architecture rules
```

why: The simulation decides the order of everything; uncontrolled concurrency makes runs unrepeatable.
