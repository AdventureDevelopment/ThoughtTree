# ADR-052: Content is untrusted input

id: ADR-052 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Domain/**`

Amended by ADR-084 (display names no longer exist); owner decision 2026-11-19.

- **MUST** treat all content (titles, notes, imported files, feedback texts) as untrusted, also in the owner's own trees
- **MUST** render Markdown without raw HTML
- **MUST** allow only the link schemes `item:`, `https:`, `http:` and `mailto:`; show every other link as plain text
- **MUST NOT** load remote pictures; show a placeholder with the address
- **MUST** convert pasted HTML (and RTF through its HTML) to Markdown with the same rules, never store it as HTML (UX decision D-62)
- **MUST** keep a collection of known attack strings that must render harmlessly, including pasted HTML
- **MUST NOT** rely on the Content Security Policy alone

verify:

```bash
dotnet test --filter "ADR=ADR-052"   # attack strings render harmlessly
```

why: In a shared tree another person writes content; injected code could read every key on the device.
