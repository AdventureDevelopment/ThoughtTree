# ADR-039: No uncontrolled concurrency or timers

id: ADR-039 · status: Accepted

applies_to: `src/**`

- **MUST NOT** use `Thread`, `Task.Run`, `Parallel`, `ThreadPool`, `System.Threading.Timer`, `System.Timers.Timer` or `Task.Delay` without a `TimeProvider` in client logic, Domain or the server core
- **MUST** do all waiting and timing through `TimeProvider` (`Task.Delay(TimeSpan, TimeProvider)`, `TimeProvider.CreateTimer`)
- **MUST** list the forbidden APIs in the banned API analyzer
- **MAY** use them in the thin hosting layers (ASP.NET Core endpoints, browser interop) where the simulation does not run

verify:

```bash
dotnet build -warnaserror   # banned APIs are compile errors
```

why: The simulation decides the order of everything; uncontrolled concurrency makes runs unrepeatable.
