namespace GitContextSwitcher.Core.Models
{
    // Lightweight summary of a saved work context. Stored in profile.json inside SavedContexts.
    // Full context details (patch, context.json, exports) are stored in the per-profile context folder.
    public class SavedWorkContext
    {
        // Unique id for the saved context
        public Guid Id { get; set; } = Guid.NewGuid();

        // FolderName is deprecated and removed; context folder is now identified by Id on disk.
        // Kept for backward-compatibility during migration but not serialized anymore.
        [System.Text.Json.Serialization.JsonIgnore]
        public string FolderName { get; set; } = string.Empty;

        // Human-entered short description for the context (provided at save time)
        public string? Description { get; set; }

        // When the context was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Optional git stash/shelve reference name recorded when the context was saved
        public string? StashRef { get; set; }

        // The number of files included in the saved context (approximation)
        public int FileCount { get; set; }

        // Patch file name (inside the context folder) for quick discovery
        public string? PatchFileName { get; set; }

        // Head commit short sha at the time of saving
        public string? HeadShortSha { get; set; }

        // Branch name at time of saving
        public string? HeadBranch { get; set; }

        // Optional per-file change list persisted into context.json to aid previews
        public System.Collections.Generic.List<ContextFileEntry>? Files { get; set; }

        // Runtime flag indicating the per-context folder is missing on disk. Not persisted.
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsFolderMissing { get; set; }
    }

    public class ContextFileEntry
    {
        public string? Path { get; set; }
        public string? Change { get; set; }
    }
}
