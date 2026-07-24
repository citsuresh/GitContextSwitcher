# Copilot Instructions — GitContextSwitcher

## Persistent Project Memory
- Before exploring the codebase with search tools for a new task, read `docs/CODE_SUMMARY.md`
  and `docs/DESIGN_DECISIONS.md` if they exist. If they don't exist, fall back to normal
  exploration — their absence is not an error.
- When the user asks "do you remember", references prior work, or asks what's next, read
  `docs/PROJECT_STATE.md` and `docs/ROADMAP.md`.
- Update `docs/CODE_SUMMARY.md` when: a new project is added, a new structural class/service is
  added, a component's responsibility changes, or a project/component dependency changes. Do not
  update for routine bug fixes or small edits that don't affect structure.
- Update `docs/DESIGN_DECISIONS.md` (append-only, dated entries) when: a non-obvious
  architectural/design choice is made, an alternative approach is rejected with a reason, or a
  past decision is reversed. Never delete prior entries.
- Update `docs/PROJECT_STATE.md` at the end of a working session to reflect current focus, open
  tasks, and recently changed files (overwrite, not append).
- Update `docs/ROADMAP.md` only when priorities/plans deliberately change, not automatically each
  session.
- Keep all four files concise — they exist to reduce token usage on future re-reads, not to serve
  as exhaustive documentation.

## Project Guidelines
- Manual commit review before any commit.
- Build/test verification after every change.
- Git commits/pushes for this repo should use the identity "Suresh Kumar Veluswamy"
  <citsuresh@rediffmail.com>.
- Do not commit or push automatically — wait for the user to review, test, and confirm.

## Response Guidelines
- Keep replies concise and minimal by default (no filler, no restating the question, no
  unnecessary preamble), EXCEPT in these cases where full detail is required:
  - Design rationale discussions.
  - Build/error diagnosis.
  - Before any destructive action.
  - When multiple approaches exist.
  - When generating docs/*.md content itself.
