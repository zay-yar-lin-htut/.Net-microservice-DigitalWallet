using LedgerService.Contracts;
using LedgerService.Data;
using LedgerService.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Services;

public sealed class LedgerService(LedgerDbContext db) : ILedgerService
{
    public async Task<LedgerEntryResponse> CreateAsync(
        Guid userId,
        CreateLedgerEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = new LedgerEntry
        {
            UserId = userId,
            ReferenceId = request.ReferenceId.Trim(),
            EntryType = request.EntryType,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            Description = request.Description?.Trim()
        };

        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(entry);
    }

    public async Task<IReadOnlyList<LedgerEntryResponse>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var entries = await db.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Select(entry => new LedgerEntryResponse(
                entry.Id,
                entry.UserId,
                entry.ReferenceId,
                entry.EntryType,
                entry.Amount,
                entry.Currency,
                entry.Description,
                entry.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return entries;
    }

    private static LedgerEntryResponse ToResponse(LedgerEntry entry) => new(
        entry.Id,
        entry.UserId,
        entry.ReferenceId,
        entry.EntryType,
        entry.Amount,
        entry.Currency,
        entry.Description,
        entry.CreatedAtUtc);
}
