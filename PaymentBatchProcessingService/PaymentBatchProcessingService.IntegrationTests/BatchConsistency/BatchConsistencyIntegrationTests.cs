
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using PaymentBatchProcessingService.App.Exceptions;
using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Infrastructure.DB;
using PaymentBatchProcessingService.Infrastructure.DB.Models;
using PaymentBatchProcessingService.Infrastructure.Stores;
using System.Security.Cryptography;
using System.Text;

namespace PaymentBatchProcessingService.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class BatchConsistencyIntegrationTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;" + 
        "Database=PaymentBatchProcessingService_IntegrationTests;" + 
        "Trusted_Connection=True;" + 
        "TrustServerCertificate=True";

    private static PaymentsDbContext CreateDbContext()
    {
        IntegrationTestDatabaseGuard.Validate(ConnectionString);

        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new PaymentsDbContext(options);
    }

    [Test]
    public async Task Concurrent_requests_with_same_idempotency_key_do_not_create_duplicate_batches()
    {
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var batchIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var paymentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var now = DateTime.UtcNow;

        var idempotencyKey = $"concurrency-{Guid.NewGuid():N}";
        var fingerprint = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("concurrency-test-request")));

        try
        {
            await using (var setupDb = CreateDbContext())
            {
                await setupDb.Database.MigrateAsync();

                setupDb.Tenants.Add(new Tenant(
                    tenantId, "Concurrency Test Tenant", now));

                setupDb.LegalEntities.Add(new LegalEntity(
                    legalEntityId, tenantId, "Concurrency Test Ltd",
                    $"IT-{Guid.NewGuid():N}", "GB", now));

                setupDb.Banks.Add(new Bank(
                    bankId, "Concurrency Test Bank", "CONCGB2LXXX", now));

                setupDb.BankAccounts.Add(new BankAccount(
                    bankAccountId, legalEntityId, bankId,
                    "GB82WEST12345698765432", Currency.EUR, now));

                await setupDb.SaveChangesAsync();
            }

            async Task<Guid> CreateAsync(int index)
            {
                await using var db = CreateDbContext();
                var store = new PaymentBatchStore(db);

                var batch = new PaymentBatch(
                    batchIds[index],
                    legalEntityId,
                    bankAccountId,
                    Currency.EUR,
                    now);

                var payment = new Payment(
                    paymentIds[index],
                    batch.Id,
                    100m,
                    "Concurrency Supplier",
                    "GB29NWBK60161331926819",
                    "CONCURRENCY-001",
                    now);

                var result = await store.CreateBatchAsync(
                    batch,
                    new[] { payment },
                    tenantId,
                    idempotencyKey,
                    fingerprint,
                    CancellationToken.None);

                return result.BatchId;
            }

            var results = await Task.WhenAll(
                CreateAsync(0),
                CreateAsync(1));

            Assert.That(results[0], Is.EqualTo(results[1]));

            await using var verificationDb = CreateDbContext();

            var records = await verificationDb.BatchIdempotencyRecords
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId &&
                            r.IdempotencyKey == idempotencyKey)
                .ToListAsync();

            Assert.That(records, Has.Count.EqualTo(1));

            var savedBatchId = records.Single().BatchId;

            Assert.Multiple(() =>
            {
                Assert.That(
                    verificationDb.PaymentBatches
                        .Count(b => b.Id == savedBatchId),
                    Is.EqualTo(1));

                Assert.That(
                    verificationDb.Payments
                        .Count(p => p.PaymentBatchId == savedBatchId),
                    Is.EqualTo(1));

                Assert.That(
                    verificationDb.PaymentBatches
                        .Count(b => batchIds.Contains(b.Id)),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();

            await cleanupDb.BatchIdempotencyRecords
                .Where(r => r.TenantId == tenantId &&
                            r.IdempotencyKey == idempotencyKey)
                .ExecuteDeleteAsync();

            await cleanupDb.Payments
                .Where(p => batchIds.Contains(p.PaymentBatchId))
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentBatches
                .Where(b => batchIds.Contains(b.Id))
                .ExecuteDeleteAsync();

            await cleanupDb.BankAccounts
                .Where(a => a.Id == bankAccountId)
                .ExecuteDeleteAsync();

            await cleanupDb.Banks
                .Where(b => b.Id == bankId)
                .ExecuteDeleteAsync();

            await cleanupDb.LegalEntities
                .Where(e => e.Id == legalEntityId)
                .ExecuteDeleteAsync();

            await cleanupDb.Tenants
                .Where(t => t.Id == tenantId)
                .ExecuteDeleteAsync();
        }
    }

    [Test]
    public async Task Failed_batch_creation_rolls_back_all_related_records()
    {
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var idempotencyKey = $"rollback-{Guid.NewGuid():N}";

        try
        {
            await using (var setupDb = CreateDbContext())
            {
                await setupDb.Database.MigrateAsync();

                setupDb.Tenants.Add(new Tenant(
                    tenantId, "Rollback Test Tenant", now));

                setupDb.LegalEntities.Add(new LegalEntity(
                    legalEntityId, tenantId, "Rollback Test Ltd",
                    $"IT-{Guid.NewGuid():N}", "GB", now));

                setupDb.Banks.Add(new Bank(
                    bankId, "Rollback Test Bank", "ROLLGB2LXXX", now));

                setupDb.BankAccounts.Add(new BankAccount(
                    bankAccountId, legalEntityId, bankId,
                    "GB82WEST12345698765432", Currency.EUR, now));

                await setupDb.SaveChangesAsync();
            }

            // The batch is valid, but its payment references a non-existent batch.
            // SQL Server must reject the payment foreign key.
            await using (var db = CreateDbContext())
            {
                var batch = new PaymentBatch(
                    batchId, legalEntityId, bankAccountId,
                    Currency.EUR, now);

                var payment = new Payment(
                    paymentId,
                    Guid.NewGuid(),
                    100m,
                    "Rollback Supplier",
                    "GB29NWBK60161331926819",
                    "ROLLBACK-001",
                    now);

                var record = new BatchIdempotencyRecord(
                    tenantId,
                    batchId,
                    idempotencyKey,
                    Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes("rollback-test-request"))),
                    now);

                db.PaymentBatches.Add(batch);
                db.Payments.Add(payment);
                db.BatchIdempotencyRecords.Add(record);

                Assert.ThrowsAsync<DbUpdateException>(
                    async () => await db.SaveChangesAsync());
            }

            await using var verificationDb = CreateDbContext();

            Assert.Multiple(() =>
            {
                Assert.That(
                    verificationDb.PaymentBatches.Count(b => b.Id == batchId),
                    Is.EqualTo(0));

                Assert.That(
                    verificationDb.Payments.Count(p => p.Id == paymentId),
                    Is.EqualTo(0));

                Assert.That(
                    verificationDb.BatchIdempotencyRecords.Count(r =>
                        r.TenantId == tenantId &&
                        r.IdempotencyKey == idempotencyKey),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();

            await cleanupDb.BatchIdempotencyRecords
                .Where(r => r.TenantId == tenantId &&
                            r.IdempotencyKey == idempotencyKey)
                .ExecuteDeleteAsync();

            await cleanupDb.Payments
                .Where(p => p.Id == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentBatches
                .Where(b => b.Id == batchId)
                .ExecuteDeleteAsync();

            await cleanupDb.BankAccounts
                .Where(a => a.Id == bankAccountId)
                .ExecuteDeleteAsync();

            await cleanupDb.Banks
                .Where(b => b.Id == bankId)
                .ExecuteDeleteAsync();

            await cleanupDb.LegalEntities
                .Where(e => e.Id == legalEntityId)
                .ExecuteDeleteAsync();

            await cleanupDb.Tenants
                .Where(t => t.Id == tenantId)
                .ExecuteDeleteAsync();
        }
    }


    [Test]
    public async Task Concurrent_requests_with_same_key_and_different_payloads_return_idempotency_conflict()
    {
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var batchIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var now = DateTime.UtcNow;

        var idempotencyKey = $"conflict-{Guid.NewGuid():N}";

        try
        {
            await using (var setupDb = CreateDbContext())
            {
                await setupDb.Database.MigrateAsync();

                setupDb.Tenants.Add(new Tenant(
                    tenantId, "Conflict Test Tenant", now));

                setupDb.LegalEntities.Add(new LegalEntity(
                    legalEntityId, tenantId, "Conflict Test Ltd",
                    $"IT-{Guid.NewGuid():N}", "GB", now));

                setupDb.Banks.Add(new Bank(
                    bankId, "Conflict Test Bank", "CONFGB2LXXX", now));

                setupDb.BankAccounts.Add(new BankAccount(
                    bankAccountId, legalEntityId, bankId,
                    "GB82WEST12345698765432", Currency.EUR, now));

                await setupDb.SaveChangesAsync();
            }

            async Task<(Guid BatchId, bool Conflict)> CreateAsync(int index)
            {
                await using var db = CreateDbContext();
                var store = new PaymentBatchStore(db);

                var batch = new PaymentBatch(
                    batchIds[index],
                    legalEntityId,
                    bankAccountId,
                    Currency.EUR,
                    now);

                var payment = new Payment(
                    Guid.NewGuid(),
                    batch.Id,
                    index == 0 ? 100m : 200m,
                    "Conflict Test Supplier",
                    "GB29NWBK60161331926819",
                    $"CONFLICT-{index}",
                    now);

                var fingerprint = Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes($"different-request-{index}")));

                try
                {
                    var result = await store.CreateBatchAsync(
                        batch,
                        new[] { payment },
                        tenantId,
                        idempotencyKey,
                        fingerprint,
                        CancellationToken.None);

                    return (result.BatchId, false);
                }
                catch (IdempotencyConflictException)
                {
                    return (Guid.Empty, true);
                }
            }

            // Each operation uses its own DbContext.
            var results = await Task.WhenAll(
                CreateAsync(0),
                CreateAsync(1));

            Assert.Multiple(() =>
            {
                Assert.That(
                    results.Count(r => r.Conflict),
                    Is.EqualTo(1),
                    "Exactly one request must encounter an idempotency conflict.");

                Assert.That(
                    results.Count(r => !r.Conflict),
                    Is.EqualTo(1),
                    "Exactly one request must create the batch.");
            });

            await using var verificationDb = CreateDbContext();

            var records = await verificationDb.BatchIdempotencyRecords
                .AsNoTracking()
                .Where(r =>
                    r.TenantId == tenantId &&
                    r.IdempotencyKey == idempotencyKey)
                .ToListAsync();

            Assert.That(records, Has.Count.EqualTo(1));

            var savedBatchId = records.Single().BatchId;
            var successfulBatchId = results.Single(r => !r.Conflict).BatchId;

            Assert.Multiple(() =>
            {
                Assert.That(
                    savedBatchId,
                    Is.EqualTo(successfulBatchId));

                Assert.That(
                    verificationDb.PaymentBatches
                        .Count(b => batchIds.Contains(b.Id)),
                    Is.EqualTo(1));

                Assert.That(
                    verificationDb.Payments
                        .Count(p => p.PaymentBatchId == savedBatchId),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();

            await cleanupDb.BatchIdempotencyRecords
                .Where(r =>
                    r.TenantId == tenantId &&
                    r.IdempotencyKey == idempotencyKey)
                .ExecuteDeleteAsync();

            await cleanupDb.Payments
                .Where(p => batchIds.Contains(p.PaymentBatchId))
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentBatches
                .Where(b => batchIds.Contains(b.Id))
                .ExecuteDeleteAsync();

            await cleanupDb.BankAccounts
                .Where(a => a.Id == bankAccountId)
                .ExecuteDeleteAsync();

            await cleanupDb.Banks
                .Where(b => b.Id == bankId)
                .ExecuteDeleteAsync();

            await cleanupDb.LegalEntities
                .Where(e => e.Id == legalEntityId)
                .ExecuteDeleteAsync();

            await cleanupDb.Tenants
                .Where(t => t.Id == tenantId)
                .ExecuteDeleteAsync();
        }
    }

}
