# ADR-060: Themes of trees and branches are shared through events

id: ADR-060 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** record the theme of a tree or of an item by an event (`theme.set` with the target tree or item and a theme id), so all devices and all users of a tree show the same theme; themes are always available, not a tree feature
- **MUST** decide concurrent `theme.set` events for the same target silently by the agreed event order (last writer wins), without `replaces` and without a conflict; the history shows both changes
- **MUST** let an item's theme apply to its whole branch, shown for all its children unless a child sets its own; otherwise the tree's theme applies
- **MUST** reference themes by fixed ids shipped with the app; **MUST** fall back to the default theme for an unknown theme id (tested in the format tests: an older app shows the default theme for a newer theme id)
- **MUST** build every component from semantic design tokens, so a theme only swaps token values (palette, font, density, note preview); **MUST** meet the contrast rules (ADR-033) in every theme, also for a tinted branch inside another theme
- **MUST NOT** treat themes as device state (ADR-013): the theme belongs to the content, like a tree's name

verify:

```bash
dotnet test --filter "ADR=ADR-060"
```

why: The owner chooses a theme to support the mood of a project; it has to look the same on every device and for everyone who shares the tree (UX decisions D-34 to D-36).
