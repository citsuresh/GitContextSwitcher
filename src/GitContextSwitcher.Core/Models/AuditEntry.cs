namespace GitContextSwitcher.Core.Models;

public class AuditEntry
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? User { get; set; }
}
