using DiscountServer.Data;
using DiscountServer.Services;
using System.Collections.Concurrent;
using Xunit;

namespace DiscountServer.Tests;

public class IntegrationTests
{
    private DiscountRepository CreateRepo()
    {
        var path = Path.GetTempFileName();
        File.Delete(path);
        return new DiscountRepository(path);
    }

    [Fact]
    public async Task Generate_2000_Codes_No_Duplicates()
    {
        var repo = CreateRepo();
        var all = new HashSet<string>();
        int attempts = 0;
        const int target = 2000;
        while (all.Count < target && attempts < 1000)
        {
            attempts++;
            var need = target - all.Count;
            var batchSize = Math.Min(need * 2, 500);
            var batch = new HashSet<string>();
            for (int i = 0; i < batchSize; i++) batch.Add(CodeGenerator.RandomCode(8));
            var inserted = await repo.InsertCodesAsync(batch, 8);
            foreach (var c in inserted)
            {
                if (all.Count >= target) 
                    break;
                all.Add(c);
            }
        }
        Assert.Equal(target, all.Count);
    }

    [Fact]
    public async Task Concurrency_Generate_Without_Duplicates_Global()
    {
        var repo = CreateRepo();
        var bag = new ConcurrentBag<string>();
        const int tasksCount = 50;
        const int perTask = 200;
        var tasks = Enumerable.Range(0, tasksCount).Select(async _ =>
        {
            var local = new HashSet<string>();
            int attempts = 0;
            while (local.Count < perTask && attempts < 1000)
            {
                attempts++;
                var need = perTask - local.Count;
                var batchSize = Math.Min(need * 2, 500);
                var batch = new HashSet<string>();

                for (int i = 0; i < batchSize; i++) 
                    batch.Add(CodeGenerator.RandomCode(7));

                var inserted = await repo.InsertCodesAsync(batch, 7);
                foreach (var c in inserted)
                {
                    if (local.Count >= perTask) 
                        break;
                    local.Add(c);
                }
            }
            foreach (var c in local) bag.Add(c);
        });
        await Task.WhenAll(tasks);
        var unique = bag.Distinct().Count();
        Assert.Equal(bag.Count, unique);
        Assert.Equal(tasksCount * perTask, unique);
    }
}
