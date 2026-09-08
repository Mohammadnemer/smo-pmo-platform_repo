using Microsoft.EntityFrameworkCore;
using SmoPmo.Platform;
using Xunit;

namespace Platform.Tests;

public sealed class TenantSeedTests
{
    [Fact]
    public async Task SeedTenantCreatesSensibleDefaults()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new PlatformDbContext(options);

        await context.SeedTenantAsync(Guid.NewGuid(), "Contoso");

        var tenant = await context.Tenants.SingleAsync();
        Assert.Equal("Contoso", tenant.Name);

        Assert.Contains(context.LookupItems, item => item.Category == "BSC Perspective" && item.Code == "Customer");
        Assert.Contains(context.LookupItems, item => item.Category == "RAID Category" && item.Code == "Risk");
        Assert.Contains(context.LookupItems, item => item.Category == "Status" && item.Code == "Draft");
        Assert.Contains(context.LookupItems, item => item.Category == "Workday" && item.Code == "Mon" && item.Value == "Monday");
        Assert.Contains(context.LookupItems, item => item.Category == "Workday" && item.Code == "Sat" && item.Value == "Saturday");
    }
}
