using DiscountServer.Data;
using System.IO;
using Xunit;

namespace DiscountServer.Tests;

public class RepositoryTests
{
    private DiscountRepository CreateRepo()
    {
        var path = Path.GetTempFileName();
        File.Delete(path); 
        return new DiscountRepository(path);
    }

    [Fact]
    public async Task InsertCodesAsync_Ignores_Duplicates()
    {
        var repo = CreateRepo();
        var codes = new[] { "AAAAAAA", "AAAAAAA", "BBBBBBB" };
        var inserted = (await repo.InsertCodesAsync(codes, 7)).ToList();
        Assert.Equal(2, inserted.Count); 
    }

    [Fact]
    public async Task UseCode_Marks_As_Used_And_Then_Fails_Second_Time()
    {
        var repo = CreateRepo();
        var code = "CCCCCCC";
        var _ = await repo.InsertCodesAsync(new[] { code }, 7);
        var first = await repo.UseCodeAsync(code);
        var second = await repo.UseCodeAsync(code);
        Assert.Equal(0, first);
        Assert.Equal(2, second); 
    }

    [Fact]
    public async Task UseCode_Returns_NotFound_For_Unknown()
    {
        var repo = CreateRepo();
        var result = await repo.UseCodeAsync("ZZZZZZZ");
        Assert.Equal(1, result); 
    }
}
