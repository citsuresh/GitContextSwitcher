namespace GitContextSwitcher.Core.Models
{
    public class WorkProfile
    {
        // Stable identifier for the profile. Use GUID so edits/renames can be tracked reliably.
        // Initialized to a new GUID for newly-created instances and for older JSON that lacks an Id.
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;
        // Description removed in favor of Notes
        // Filesystem path of the repository associated with this profile (optional)
        public string? RepoPath { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public string PatchFilePath { get; set; } = string.Empty;
        public string? StashRef { get; set; }
        public List<ProfileFileEntry> Files { get; set; } = new();
        public List<AuditEntry> AuditHistory { get; set; } = new();
    }
}