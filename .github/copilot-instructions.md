# Copilot Instructions — GitContextSwitcher

<!-- project-memory-management-graph: skill-version=9 -->
## Persistent Project Memory
- Before exploring the codebase with search tools for a new task, read `docs/CODE_SUMMARY.md`
  and `docs/DESIGN_DECISIONS.md` if they exist. If they don't exist, fall back to normal
  exploration — their absence is not an error.
- When the user asks "do you remember", references prior work, or asks what's next, read
  `docs/PROJECT_STATE.md` and `docs/ROADMAP.md`.
- If it exists, read `docs/domain-lookup-patterns.md` when a task requires domain conventions,
  naming schemes, or business logic that the graph doesn't represent — check it before falling
  back to manual exploration or a fresh graph query.
- `docs/full-graph.json`/`docs/project-dependencies.json` are available and should be queried via
  the GraphTools wrapper script (located at
  `C:\MyFiles\Git\GraphTools\tools\Invoke-GraphTools.ps1`, invoked as
  `Invoke-GraphTools.ps1 -Tool Query -- <args>`) (never read wholesale). This is a default, not a
  judgment call: before using a general-purpose search tool (text search, symbol search, grep, or
  similar) to locate a class/interface/enum, find a method's definition, find its callers, find
  its callees, check how two types relate, or otherwise answer "where is X" / "what uses X" for
  anything that is a C# symbol, first check whether `docs/full-graph.json` exists in this
  project, and if so, query it via the wrapper script instead of a general search tool. This
  applies even to a simple "find this file/class" request, not only explicit call-graph or
  architecture questions, and applies regardless of how the question is phrased —
  conceptual/explanatory framings ("explain X", "how does X work") are not exempt just because
  they aren't literally worded as a find/locate request.
  Fallback: if `docs/full-graph.json` does not exist, if the query errors or exits non-zero, or
  if the graph doesn't contain an answer to the specific question (e.g. the question is about
  non-code content, file layout, or something the graph doesn't track), fall back to normal
  search tools and proceed — do not treat a missing/failed graph query as a blocking error.
- Update `docs/CODE_SUMMARY.md` when: a new project is added, a new structural class/service is
  added, a component's responsibility changes, or a project/component dependency changes. Do not
  update for routine bug fixes or small edits that don't affect structure. Ensure the pointer
  line to `docs/KEY_FLOWS.md` stays present; do not add Key Flows entries directly to
  `docs/CODE_SUMMARY.md`.
- Update `docs/KEY_FLOWS.md` directly (not `docs/CODE_SUMMARY.md`) when a new end-to-end flow
  spanning multiple C# symbols is fully traced and confirmed: add it as a short arrow-chain
  (e.g., `A -> B -> C -> D`). If a relevant `docs/domain-lookup-patterns.md` entry already exists
  for that flow, append a short one-line pointer to it — never pull domain-specific details
  themselves into `docs/KEY_FLOWS.md`.
- Update `docs/DESIGN_DECISIONS.md` (append-only, dated entries) when: a non-obvious
  architectural/design choice is made, an alternative approach is rejected with a reason, or a
  past decision is reversed. Never delete prior entries.
- Update `docs/PROJECT_STATE.md` at the end of a working session to reflect current focus, open
  tasks, and recently changed files (overwrite, not append).
- Update `docs/ROADMAP.md` only when priorities/plans deliberately change, not automatically each
  session.
- `docs/KNOWN_OPEN_FINDINGS.md`, if present, is a purely user-controlled reference list of open
  findings the user chose not to act on immediately — never add, edit, or remove entries without
  the user explicitly asking to.
- Keep all memory files concise — they exist to reduce token usage on future re-reads, not to
  serve as exhaustive documentation.

## Project Guidelines
- Manual commit review before any commit.
- Build/test verification after every change.
- Do not commit or push automatically — wait for the user to review, test, and confirm.

## Response Guidelines
- Keep replies concise and minimal by default (no filler, no restating the question, no
  unnecessary preamble), EXCEPT in these cases where full detail is required:
  - Design rationale discussions.
  - Build/error diagnosis.
  - Before any destructive action.
  - When multiple approaches exist.
  - When generating docs/*.md content itself.
