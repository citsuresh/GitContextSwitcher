# Project State

> Overwritten (not appended) at the end of each working session. Reflects current focus, open
> tasks, and recently changed files as of the last session.

## Current Focus
Designed and wired in an app icon (branch-fork glyph over stacked context cards, blue/green) as
`src/GitContextSwitcher.UI/app_icon.ico` (7 embedded sizes 16-256px; the first draft only embedded
a 16px frame, causing blurry upscaling at large icon sizes — fixed by re-saving with all sizes
explicitly). Wired via `ApplicationIcon` in the UI `.csproj` and `Icon="app_icon.ico"` on
`MainWindow.xaml`. Cleaned up unused draft icon variants from `assets/`, keeping only the chosen
icon's source files. Updated `README.md` (was stale "initial scaffold" text) to describe the app,
its projects, and getting-started steps.
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
- `README.md` — rewritten from stale "initial scaffold" placeholder to an accurate project
  description, project list, and getting-started steps.
- `src/GitContextSwitcher.UI/GitContextSwitcher.UI.csproj` — added `ApplicationIcon` and a
  `Resource` entry for `app_icon.ico`; fixed indentation.
- `src/GitContextSwitcher.UI/MainWindow.xaml` — added `Icon="app_icon.ico"`.
- `src/GitContextSwitcher.UI/app_icon.ico` — new app icon (7 sizes, 16-256px).
- `assets/app_icon.ico`, `assets/app_icon_source.png` — source/reference copies of the chosen
  icon design; unused draft variants removed.

## Session Notes
- Row copy relies on WPF `DataGrid`'s built-in `ApplicationCommands.Copy`; `DataGridBoundColumn`s
  copy automatically, but `DataGridTemplateColumn`s need an explicit `ClipboardContentBinding` to
  be included in the copied text — easy to miss when a grid mixes bound and templated columns.
- When generating multi-size `.ico` files with Pillow, always verify the embedded sizes
  afterward (e.g. parse the ICONDIR header) — `Image.save(..., format="ICO", sizes=[...])` can
  silently produce a single-size file if the source image resolution or resize list isn't handled
  as expected, leading to blurry upscaling in large icon views.
