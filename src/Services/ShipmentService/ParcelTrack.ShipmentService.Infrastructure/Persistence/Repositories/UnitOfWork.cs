using ParcelTrack.ShipmentService.Application.Interfaces;

namespace ParcelTrack.ShipmentService.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ShipmentDbContext _context;

    public UnitOfWork(ShipmentDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine(_context.ChangeTracker.DebugView.LongView);
        var entries = _context.ChangeTracker.Entries();

        foreach (var entry in entries)
        {
            Console.WriteLine($"{entry.Entity.GetType().Name} => {entry.State}");
        }

        return await _context.SaveChangesAsync(cancellationToken);
    }
}
