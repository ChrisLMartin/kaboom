using Microsoft.EntityFrameworkCore;
using Kaboom.Data;

namespace Kaboom.Services;

public sealed class DatabaseBootstrapper
{
    private readonly KaboomDbContext _dbContext;

    public DatabaseBootstrapper(KaboomDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
