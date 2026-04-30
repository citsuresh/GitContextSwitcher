namespace GitContextSwitcher.Core.Models
{
    public class WorkProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
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