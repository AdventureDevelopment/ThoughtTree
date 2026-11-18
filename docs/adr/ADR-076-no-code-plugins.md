# ADR-076: No code plugins; extend by data, readable files or a fork

id: ADR-076 · status: Proposed · kind: constraint

applies_to: `src/**`

Owner decision 2026-11-17 (Zettelkasten session), proposed for acceptance in a pull request review.

- **MUST NOT** load or run code that is not part of the app's own build: no plugin or extension mechanism, no loading of assemblies, scripts or WebAssembly modules at run time from user content, files, trees or addresses
- **MUST NOT** add an interface through which outside code reads decrypted content or uses the key handle
- **MAY** offer extension by data only, interpreted by the app's own code with fixed rules: templates (ADR-068), views (ADR-062), relation types, tags, themes shipped with the app (ADR-060), and declarative import profiles
- **SHOULD** name the readable export and import (ADR-019) as the way to work on a tree with other tools, outside the app, and back
- **MUST** leave custom code to forks: the code is Apache 2.0 (ADR-049) and a self-hosted package serves its own app with its own policy on its own origin (ADR-045); the hosted app never widens its policy for it

verify:

```bash
dotnet test --filter "ADR=ADR-076"   # architecture test: no run-time loading of assemblies or modules (for example Assembly.Load, AssemblyLoadContext, dynamic import) outside the app's own startup
```

why: Code inside the app sees decrypted content and the key handle; in a shared tree it reads the thoughts of people who never agreed to it, so a warning to the installer cannot make it safe; it would also break the strict CSP (ADR-045), the bans on AI (ADR-071) and third-party requests (ADR-030), and the tested formats.
