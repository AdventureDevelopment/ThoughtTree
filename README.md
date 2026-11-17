# ThoughtTree

A hierarchical note-taking app for structuring thoughts as an outline tree. It runs in the browser as an installable Progressive Web App (PWA), built with Blazor WebAssembly and C#.

> **Status: foundation stage.** The architecture, the decisions and the UX concept are written down, and the project skeleton with its test harness builds. There are no features yet and nothing to install or try.

## What it is meant to be

- **Touch-first outliner** for phone and tablet, with keyboard shortcuts on desktop. Unlimited nesting, quick capture, drag and drop, focus on an item, relations between items.
- **Standalone first.** A device works fully on its own, with no account, no server and no internet. Pairing devices and syncing are optional. Release 1 syncs by encrypted file.
- **Private by design.** Notes are encrypted on the device with end-to-end encryption; a server, if you use one, only relays ciphertext. There is no telemetry, and no AI reads your thoughts.
- **Your data stays yours.** Readable export and import (Markdown, OPML, JSON). The architecture is event-sourced, so history is kept and old data stays readable.
- **Accessible and bilingual:** English and German; screen reader, keyboard and contrast support are part of the quality bar.

## Where to read more

| What | Where |
| --- | --- |
| Architecture (arc42): goals, building blocks, test harness, open decisions | [docs/architecture.md](docs/architecture.md) |
| Architecture decision records (ADRs), written for humans and coding agents | [docs/adr/](docs/adr/README.md) |
| UX concept: principles, terms, decisions D-1 to D-165 | [docs/ux-concept.md](docs/ux-concept.md) |
| Sketches and the clickable HTML prototype | [docs/ux/](docs/ux/) |
| Plan for the first spike (storage engine and AOT) | [docs/spikes/spike-1-storage-and-aot.md](docs/spikes/spike-1-storage-and-aot.md) |
| Independent consistency review of the documents | [docs/review/](docs/review/) |

## Build and test

You need the .NET SDK pinned in [global.json](global.json) (10.0.x). From the repository root:

```bash
dotnet build ThoughtTree.slnx -warnaserror
dotnet test ThoughtTree.slnx
```

`eng/branch-check.sh` runs the same steps as the CI branch check. More commands, such as running the tests of one ADR, are in [AGENTS.md](AGENTS.md).

## How the project is built

Most of the code is written by AI coding agents under rules that are checked mechanically: architecture tests, banned APIs, and specifications that are written first. If you work on this repository with an agent, or as an agent, start with [AGENTS.md](AGENTS.md). Changes reach `main` only through pull requests; protected paths, including all documentation and the ADRs, need the code owner's review (see [.github/CODEOWNERS](.github/CODEOWNERS)). Pull requests use the [template](.github/pull_request_template.md).

## Licence

Licensed under the [Apache License 2.0](LICENSE). The name "ThoughtTree" is not licensed; see [NOTICE](NOTICE).
