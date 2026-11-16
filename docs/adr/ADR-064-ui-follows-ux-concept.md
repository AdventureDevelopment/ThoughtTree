# ADR-064: UI work follows the UX concept

id: ADR-064 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `tests/specify/**`

- **MUST** read the UX principles, the terms and the decisions of [ux-concept.md](../ux-concept.md) that concern the screen before changing UI behaviour or look; where a decision and the code differ, the decision wins until the owner changes it
- **MUST** show an HTML sketch to the owner before building a new screen or a visibly new part of one, and build only the chosen variant; store the chosen sketch in `docs/ux/sketches/` and link it from its decision (UX decisions D-46, D-47)
- **MUST** use only the semantic design tokens for colour, font, spacing and density; **MUST NOT** use literal colours or sizes in components
- **MUST** name the UX decisions a UI specification secures (for example `D-12`) in the specification, as ADR ids are named in tests
- **MUST** attach to every pull request that changes UI: screenshots generated from the UI specifications (phone and tablet, light and dark), the link to the chosen sketch, and the link to the pull request's preview address (Azure Static Web Apps, ADR-047)
- **MUST** fill the UI checklist of the pull request template (`.github/pull_request_template.md`, created together with the agent guide): principles checked, terms used, tokens only, sketch linked, screenshots attached, keyboard paths and screen reader names, roles and announcements (checked by specs and component rules) covered
- **MUST** point to this ADR and to the UX concept from the agents' entry file (`CLAUDE.md` or `AGENTS.md`), so every agent starting UI work reads them

verify:

```bash
grep -rnE '#[0-9a-fA-F]{3,8}\b' --include=*.razor --include=*.css src/ThoughtTree.Client | grep -v tokens | wc -l   # expect 0: no literal colours outside the token files
dotnet test --filter "ADR=ADR-064"   # every UI specification names at least one UX decision
```

why: UX intent reaches agents only through what they are pointed to; a binding rule, a pull request checklist and the entry file together act as the definition of done for UI work (UX decision D-48).
