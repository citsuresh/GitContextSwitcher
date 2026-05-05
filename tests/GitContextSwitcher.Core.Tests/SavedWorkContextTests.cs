using GitContextSwitcher.Core.Models;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace GitContextSwitcher.Core.Tests;

public class SavedWorkContextTests
{
    [Fact]
    public void SavedWorkContext_Defaults()
    {
        var sc = new SavedWorkContext();
        Assert.NotEqual(System.Guid.Empty, sc.Id);
        Assert.False(string.IsNullOrWhiteSpace(sc.FolderName));
        Assert.True(sc.CreatedAt <= System.DateTime.UtcNow);
    }
}
