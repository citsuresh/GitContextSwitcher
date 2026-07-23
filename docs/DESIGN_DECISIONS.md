# Design Decisions

> Append-only, dated log of non-obvious architectural/design choices. Never delete or rewrite
> prior entries — if a decision is reversed, add a new entry referencing the old one.

## 2026-04-30 — Git access via CLI, not a library binding
- **Decision**: Use the `git` CLI (via `GitExportHelper`/`GitCliService` shelling out to `git.exe`) for all repository operations (status, export, patch, blob content) instead of a managed Git library such as LibGit2Sharp.
- **Rationale**: Avoids native binary dependencies/packaging complexity for a WPF desktop tool; relies on the user's already-installed Git.
- **Alternatives considered**: LibGit2Sharp (managed bindings) — rejected to avoid native interop and NuGet native asset complexity.

## 2026-04-30 — Per-profile folder storage under LocalAppData
- **Decision**: Persist profiles and saved contexts under `%LocalAppData%\GitContextSwitcher\profiles\<profileGuid>\SavedContexts\<contextGuid>\...` (see `AppPaths`, `ProfileStorageManager`, `ProfileFileStore`).
- **Rationale**: Keeps state isolated per profile, avoids collisions, and matches typical Windows desktop app data conventions.
- **Alternatives considered**: Single shared JSON file for all profiles — rejected due to concurrency/locking complexity and harder per-profile cleanup.

## 2026-04-30 — Saved-context preview: left side sourced from local repo working tree, not repo HEAD/blob
- **Decision**: In the saved-context preview diff (`PreviewViewModel`/`PreviewPane`), the left side of the diff is read from the local repository working tree path (not fetched from `git show`/HEAD blob), with an explicit warning banner noting it may contain local changes.
- **Rationale**: After repeated attempts to fetch left-side content from repo HEAD/blob resulted in empty or incorrect content (see conversation history), reverting to local working-tree content was simpler and more reliable; the warning banner communicates the tradeoff to the user instead of hiding it.
- **Alternatives considered**: Resolving left-side content via `git show <commit>:<path>` at the time the context was saved — reverted due to empty-content bugs and added complexity; may be revisited later.

## 2026-04-30 — UI-bound ObservableCollection mutations must stay on the Dispatcher thread
- **Decision**: `PreviewViewModel.LoadAsync()` guards against being invoked off the UI thread (marshals via `Dispatcher.InvokeAsync` if needed) and avoids `ConfigureAwait(false)` on awaits that precede mutations of `Files`/`FileTree`/`FileTreeNode.Children`.
- **Rationale**: A `System.NotSupportedException` was thrown because a `CollectionView`-bound `ObservableCollection` was mutated from a non-Dispatcher thread (a background continuation after an awaited call configured with `ConfigureAwait(false)`).
- **Alternatives considered**: Building the tree entirely off-thread into plain lists then assigning wholesale on the UI thread — considered more complex than simply keeping the async continuations on the UI `SynchronizationContext`; the simpler fix was adopted.

## 2026-04-30 — Extracted shared GitChangeKind display logic into GitChangeKindDisplay
- **Decision**: Added `GitChangeKindDisplay` (static helper in `GitContextSwitcher.UI.ViewModels`) with `GetDisplayIcon`, `GetPendingDisplayIcon`, `GetIconBrush`, `GetDisplaySuffix`, and `GetPendingDisplaySuffix`. `PreviewViewModel.FileTreeNode` and `ProfileTabViewModel.FileTreeNode` now delegate their `DisplayIcon`/`IconBrush`/`DisplaySuffix` properties to this helper instead of each containing nearly-identical `switch` expressions over `GitChangeKind`.
- **Rationale**: The two `FileTreeNode` classes had duplicated icon glyph, icon brush, and suffix-text logic (identical color/glyph mappings), which risked drifting out of sync if only one was updated. Consolidating reduces duplication while preserving each view's distinct historical behavior (e.g. the pending-changes tree shows no icon for directory/group nodes with no `Entry`, while the preview tree shows a "▸" glyph for directories).
- **Alternatives considered**: Fully unifying the two `FileTreeNode` classes into one shared type — deferred because their tree-building algorithms differ meaningfully (simple path-splitting in `PreviewViewModel` vs. staged/unstaged grouping in `ProfileTabViewModel`); only the display-logic duplication was addressed for now.
