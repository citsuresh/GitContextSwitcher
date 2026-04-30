using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.Core.Services;

public interface IWorkProfileService
{
    Task<WorkProfile> CreateProfileAsync(string name, string? notes = null, CancellationToken cancellationToken = default);
    Task SaveProfileAsync(WorkProfile profile, CancellationToken cancellationToken = default);
    Task<WorkProfile?> LoadProfileAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkProfile>> ListProfilesAsync(CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(string name, CancellationToken cancellationToken = default);
}
