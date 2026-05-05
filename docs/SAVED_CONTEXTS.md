Saved Work Contexts - Design & Implementation Notes

Overview

Saved Work Contexts allow the user to snapshot their current working tree (including untracked files) inside a Work Profile without creating a commit or branch. Each saved context consists of:

- A per-profile contexts/<contextId>/ folder containing:
  - context.json - metadata (SavedWorkContext)
  - changes.patch - unified patch representing diffs and new files
  - files/ - optional copy of files included in the context for quick inspection or export

Key behaviors

- Creation
  - The UI creates a lightweight SavedWorkContext summary and inserts it into WorkProfile.SavedContexts. The entry is persisted to profile.json when the profile is saved.
  - SaveContextAsync will create the context folder, export the files, create a unified patch (including untracked file add hunks), and create a git stash (including untracked files) to preserve working tree state.
  - If any operation fails, the folder is deleted and a history entry is created describing the failure. If a stash was created before failure, the stash reference is recorded in history so the user can restore manually.

- Storage location
  - Per-profile folders live under %LOCALAPPDATA%\GitContextSwitcher\profiles\<profileId>\contexts\<contextId>\
  - profile_master.json maps profile ids to per-profile folders.

- Stash lifecycle
  - SaveContextAsync creates a stash with message prefix "GCS: <description>" and records the returned SHA in SavedWorkContext.StashRef.
  - The implementation currently leaves the stash intact; restore/cleanup flows will be implemented later with explicit user confirmation.

- Discovery and UI
  - ProfileTabControl shows a "Saved Work Contexts" expander with Save and Refresh actions and a DataGrid listing SavedContexts from WorkProfile.SavedContexts.
  - The Save action shows a small dialog to capture an optional description, then performs the save operation and writes context metadata.

Follow-ups

- Restore/Apply UX: implement preview, apply, and restore with user confirmation. Decide whether to auto-apply stash or to prompt.
- Delete & Purge: implement deletion with optional stash drop.
- Export: add zip-on-demand to package context for sharing.
- Background processing: long-running export operations should be cancellable with progress.

Testing notes

- Unit test the small components: SavedWorkContext serialization, ProfileStorageManager Create/Delete context folder, context.json read/write.
- Integration test the CreateUnifiedPatchAsync for a small repo sample with an untracked file to verify patch contents include a new-file hunk.

Security & safety

- All file writes are performed under the per-profile folder; no writes are made to the repository working tree by this feature.
- The stash creation requires git CLI to be present and may not be possible on read-only worktrees.


