using Microsoft.EntityFrameworkCore;
using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.Infrastructure.DB.Seed;

public static class DevelopmentDataSeeder
{
    public static readonly Guid TenantAId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid TenantBId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid LegalEntityAId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static readonly Guid BankAccountAId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static readonly Guid LegalEntityBId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    public static readonly Guid BankAccountBId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private static readonly Guid BankId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    public static async Task SeedAsync(
        PaymentsDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var createdAtUtc = new DateTime(
            2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        if (!await dbContext.Set<Tenant>().AnyAsync(x => x.Id == TenantAId, cancellationToken))
        {
            dbContext.Set<Tenant>().Add(new Tenant(TenantAId, "Tenant A", createdAtUtc));
        }

        if (!await dbContext.Set<Tenant>().AnyAsync(x => x.Id == TenantBId, cancellationToken))
        {
            dbContext.Set<Tenant>().Add(new Tenant(TenantBId, "Tenant B", createdAtUtc));
        }

        if (!await dbContext.Set<Bank>()
                .AnyAsync(x => x.Id == BankId, cancellationToken))
        {
            dbContext.Set<Bank>().Add(new Bank(BankId, "Example Bank", "WESTGB2LXXX", createdAtUtc));
        }

        if (!await dbContext.Set<LegalEntity>().AnyAsync(x => x.Id == LegalEntityAId, cancellationToken))
        {
            dbContext.Set<LegalEntity>().Add(new LegalEntity(LegalEntityAId, TenantAId, "Example Payments Ltd", "REG-A-001", "GB", createdAtUtc));
        }

        if (!await dbContext.Set<LegalEntity>().AnyAsync(x => x.Id == LegalEntityBId, cancellationToken))
        {
            dbContext.Set<LegalEntity>().Add(new LegalEntity(LegalEntityBId, TenantBId, "Another Payments Ltd", "REG-B-001", "GB", createdAtUtc));
        }

        if (!await dbContext.Set<BankAccount>().AnyAsync(x => x.Id == BankAccountAId, cancellationToken))
        {
            dbContext.Set<BankAccount>().Add(new BankAccount(BankAccountAId, LegalEntityAId, BankId, "GB82WEST12345698765432", Currency.EUR, createdAtUtc));
        }

        if (!await dbContext.Set<BankAccount>().AnyAsync(x => x.Id == BankAccountBId, cancellationToken))
        {
            dbContext.Set<BankAccount>().Add(new BankAccount(BankAccountBId, LegalEntityBId, BankId, "GB29NWBK60161331926819", Currency.USD, createdAtUtc));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}