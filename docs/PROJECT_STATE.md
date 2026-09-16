# Project State

> Overwritten (not appended) at the end of each working session. Reflects current focus, open
> tasks, and recently changed files as of the last session.

## Current Focus
Re-synced project memory to `project-memory-management-graph` skill v10 (Bootstrap re-run:
version marker bumped 9→10, graph rebuilt to 582 nodes/2034 edges). Then added clipboard/copy
UX improvements: (1) selected-row copy-to-clipboard support for the Saved Work Contexts and
History `DataGrid`s via a new shared `CopyableDataGridStyle` (Ctrl+C / right-click "Copy",
extended full-row selection), including `ClipboardContentBinding` on the template columns
(Description, Notes, Error) so their text is included in the copied output; (2) made the
Preview window's "Context details" name/value tree selectable/copyable by swapping the
read-only `TextBlock`s for borderless read-only `TextBox`es. Build verified after stopping the
running debug instance that was locking output DLLs.
Next up: implement roadmap item to make the saved-context preview left-side diff source from
repo HEAD/blob instead of the local working tree (unchanged from before this session).

## Open Tasks / Known Issues
- Implement preview left-side diff sourced from repo HEAD/blob (via `git show <sha>:<path>`)
  instead of local working tree, with graceful fallback (to current local-path + warning banner
  behavior) if the stored commit SHA no longer exists in the repo (e.g. after rebase/GC), and
  correct handling for Added (not present at HEAD) / Deleted (present at HEAD, absent now) files.
- Consider extending `CopyableDataGridStyle` to any other read-only grids added in the future
  (currently applied to Saved Work Contexts and History grids only).

## Recently Changed Files
- `.github/copilot-instructions.md` — skill-version marker bumped 9→10.
- `docs/full-graph.json`, `docs/project-dependencies.json` — rebuilt (582 nodes / 2034 edges).
- `src/GitContextSwitcher.UI/Themes/SharedStyles.xaml` — new `CopyableDataGridStyle` +
  `DataGridCopyContextMenu` for row copy-to-clipboard support.
- `src/GitContextSwitcher.UI/Views/ProfileTabControl.xaml` — applied `CopyableDataGridStyle` to
  the Saved Contexts and History grids; added `ClipboardContentBinding` to their template
  columns (Description; Notes, Error).
- `src/GitContextSwitcher.UI/Views/PreviewWindow.xaml` — context-details tree now uses
  selectable/copyable read-only `TextBox`es instead of `TextBlock`s.

## Session Notes
- Row copy relies on WPF `DataGrid`'s built-in `ApplicationCommands.Copy`; `DataGridBoundColumn`s
  copy automatically, but `DataGridTemplateColumn`s need an explicit `ClipboardContentBinding` to
  be included in the copied text — easy to miss when a grid mixes bound and templated columns.
