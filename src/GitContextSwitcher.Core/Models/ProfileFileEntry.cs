namespace GitContextSwitcher.Core.Models;

public class ProfileFileEntry
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsTracked { get; set; }
    public bool IsModified { get; set; }
    public bool IsUntracked { get; set; }
    public long? FileSize { get; set; }
    public DateTime? LastModified { get; set; }
}
