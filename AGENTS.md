# AGENTS (Repository Root)

This file is **authoritative and persistent** for everything under `/ReferenceReplacementMod`. Keep it in version control and treat its directives as mandatory.

## Directives

1. **Golden rule:** Relentlessly minimize complexity, maximize changeability, and enforce strict static checks to suppress runtime bugs. Names, code, and directory structures must be self-explanatory—avoid relying on comments/docs for basic comprehension.
2. **Testing cadence:** Hands-on runtime testing happens later in a live Resonite session; until then, document assumptions and leave TODOs rather than speculative fixes.
3. **Localization:** UI remains English-only until Resonite exposes extensible localization hooks. Do not ship partial translations; revisit only when upstream enables custom locale registration.
4. **Knowledge management:** Split knowledge based on disclosure level.
   - Public/committable notes (design decisions, checklists) belong in versioned docs like this AGENTS file or other repo docs.
   - Private, potentially sensitive research (e.g., raw assembly dumps, exploratory notes) must live under the git-ignored `local_notes/` directory, each topic in its own markdown file to keep knowledge granular.
5. **Persistence:** Any change to policies/workflow must be reflected here immediately. Subdirectories inherit these rules unless they define their own `AGENTS.md`.
6. **Data model constraints:** This mod must not introduce new FrooxEngine data-model types or `SyncDelegate` definitions. All functionality must be built on existing data-model constructs to avoid sync registration overhead.

## Scope / Status

- Repository initialized 2025-11-12 with Reference Replacement mod for ResoniteModLoader.
- Current deliverable: Userspace dialog for bulk `ISyncRef` replacement, launched exclusively from the `Create New > Editor` menu entry.

## Localization Policy (Public Summary)

- English-only interface. Reevaluate once Resonite core supports custom localized strings for Dev Tool menus and UIX builders.

## History Tracking Rule

- Detailed work history lives in the git commit log; do not maintain manual work logs in docs. Summaries belong in commit messages and PR descriptions only.
