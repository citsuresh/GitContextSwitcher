using System.Collections.Generic;
using System.Threading.Tasks;
using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.UI.Services
{
    public interface IProfileStore
    {
        Task<List<WorkProfile>> LoadAsync();
        Task SaveAsync(List<WorkProfile> profiles);
    }
}
