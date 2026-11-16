# ADR-061: System font first, shipped fonts under the SIL Open Font License

id: ADR-061 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `**/*.csproj`, `**/package.json`

- **MUST** use the device's system font as the base font, so the first load downloads no font
- **MUST** ship every other font with the app from its own origin (ADR-045); **MUST NOT** load fonts from third parties
- **MUST** download shipped fonts in the background after start and keep them for offline use; until a theme's font is available, show the system font
- **MUST** download a font only when a theme in use on this device needs it, not all shipped fonts at once
- **MAY** use fonts under the SIL Open Font License 1.1 (OFL); approves OFL for fonts as a "similar permissive licence" of ADR-050; the push gate's licence check recognises font files
- **MUST** keep fonts out of the size budget of the first load (PF5), with their own budget for the background download

verify:

```bash
dotnet test --filter "ADR=ADR-061"   # no font requested before the app is usable; every font file has an approved licence
```

why: The system font keeps startup fast; project themes may bring their own font (UX decision D-37), and nearly all good open fonts use the OFL.
