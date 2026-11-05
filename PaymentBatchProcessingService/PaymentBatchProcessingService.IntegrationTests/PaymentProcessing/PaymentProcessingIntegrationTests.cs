using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.App.PaymentProcessing;
using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Infrastructure.Banking;
using PaymentBatchProcessingService.Infrastructure.DB;
using PaymentBatchProcessingService.Infrastructure.Stores;

namespace PaymentBatchProcessingService.IntegrationTests.PaymentProcessing;

[TestFixture]
[NonParallelizable] //DB is shared across tests, so run sequentially to avoid conflicts
public sealed class PaymentProcessingIntegrationTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;" +
        "Database=PaymentBatchProcessingService_IntegrationTests;" +
        "Trusted_Connection=True;" +
        "TrustServerCertificate=True;";

    private static PaymentsDbContext CreateDbContext()
    {
        IntegrationTestDatabaseGuard.Validate(ConnectionString);

        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new PaymentsDbContext(options);
    }

    [Test]
    public async Task Database_can_be_created_and_migrated()
    {
        await using var dbContext = CreateDbContext();

        await dbContext.Database.MigrateAsync();

        Assert.That(
            await dbContext.Database.CanConnectAsync(),
            Is.True);
    }

    [Test]
    public async Task Processing_all_payments_completes_batch_and_persists_audit_history()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var sourceBankAccountId = Guid.NewGuid();

        var batchId = Guid.NewGuid();
        var payment1Id = Guid.NewGuid();
        var payment2Id = Guid.NewGuid();

        var createdAtUtc = DateTime.UtcNow.AddMinutes(-1);

        var bic = "TESTGB2LXXX";

        try
        {
            await using (var setupDb = CreateDbContext())
            {
                await setupDb.Database.MigrateAsync();

                var tenant = new Tenant(
                    tenantId,
                    "Integration Test Tenant",
                    createdAtUtc);

                var legalEntity = new LegalEntity(
                    legalEntityId,
                    tenantId,
                    "Integration Test Ltd",
                    $"IT-{Guid.NewGuid():N}",
                    "GB",
                    createdAtUtc);

                var bank = new Bank(
                    bankId,
                    "Integration Test Bank",
                    bic,
                    createdAtUtc);

                var sourceAccount = new BankAccount(
                    sourceBankAccountId,
                    legalEntityId,
                    bankId,
                    "GB82WEST12345698765432",
                    Currency.EUR,
                    createdAtUtc);

                var batch = new PaymentBatch(
                    batchId,
                    legalEntityId,
                    sourceBankAccountId,
                    Currency.EUR,
                    createdAtUtc);

                var payment1 = new Payment(
                    payment1Id,
                    batchId,
                    125.50m,
                    "Supplier One",
                    "GB82WEST12345698765432",
                    "INTEGRATION-001",
                    createdAtUtc);

                var payment2 = new Payment(
                    payment2Id,
                    batchId,
                    275.75m,
                    "Supplier Two",
                    "GB29NWBK60161331926819",
                    "INTEGRATION-002",
                    createdAtUtc.AddSeconds(1));

                setupDb.Tenants.Add(tenant);
                setupDb.LegalEntities.Add(legalEntity);
                setupDb.Banks.Add(bank);
                setupDb.BankAccounts.Add(sourceAccount);
                setupDb.PaymentBatches.Add(batch);
                setupDb.Payments.AddRange(payment1, payment2);

                await setupDb.SaveChangesAsync();
            }

            // Act: process the first payment.
            await using (var db = CreateDbContext())
            {
                var service = new ProcessPaymentService(
                    new PaymentProcessingStore(db, TimeProvider.System),
                    new FakeBankPaymentGateway(),
                    TimeProvider.System,
                    NullLogger<ProcessPaymentService>.Instance);

                var processed = await service.ProcessNextAsync(
                    CancellationToken.None);

                Assert.That(processed, Is.True);
            }

            // Act: process the second payment using a fresh DbContext.
            await using (var db = CreateDbContext())
            {
                var service = new ProcessPaymentService(
                    new PaymentProcessingStore(db, TimeProvider.System),
                    new FakeBankPaymentGateway(),
                    TimeProvider.System,
                    NullLogger<ProcessPaymentService>.Instance);

                var processed = await service.ProcessNextAsync(
                    CancellationToken.None);

                Assert.That(processed, Is.True);
            }

            // Assert: reload all persisted data from SQL Server.
            await using (var verificationDb = CreateDbContext())
            {
                var batch = await verificationDb.PaymentBatches
                    .AsNoTracking()
                    .SingleAsync(b => b.Id == batchId);

                var payments = await verificationDb.Payments
                    .AsNoTracking()
                    .Where(p => p.PaymentBatchId == batchId)
                    .ToListAsync();

                var attempts = await verificationDb.PaymentAttempts
                    .AsNoTracking()
                    .Where(a => a.PaymentId == payment1Id ||
                                a.PaymentId == payment2Id)
                    .ToListAsync();

                var history = await verificationDb.PaymentStatusHistories
                    .AsNoTracking()
                    .Where(h => h.PaymentId == payment1Id ||
                                h.PaymentId == payment2Id)
                    .ToListAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        batch.Status,
                        Is.EqualTo(PaymentBatchStatus.Completed));

                    Assert.That(
                        payments,
                        Has.Count.EqualTo(2));

                    Assert.That(
                        payments.All(p =>
                            p.Status == PaymentStatus.Submitted),
                        Is.True);

                    Assert.That(
                        payments.All(p =>
                            !string.IsNullOrWhiteSpace(p.BankReference)),
                        Is.True);

                    Assert.That(
                        attempts,
                        Has.Count.EqualTo(2));

                    Assert.That(
                        attempts.All(a =>
                            a.Outcome == PaymentAttemptOutcome.Accepted),
                        Is.True);

                    Assert.That(
                        history,
                        Has.Count.EqualTo(4));

                    Assert.That(
                        history.Count(h =>
                            h.FromStatus == PaymentStatus.Accepted &&
                            h.ToStatus == PaymentStatus.Processing),
                        Is.EqualTo(2));

                    Assert.That(
                        history.Count(h =>
                            h.FromStatus == PaymentStatus.Processing &&
                            h.ToStatus == PaymentStatus.Submitted),
                        Is.EqualTo(2));
                });
            }
        }
        finally
        {
            // Delete only records created by this test.
            // Delete children before parents to respect foreign keys.
            await using var cleanupDb = CreateDbContext();

            await cleanupDb.PaymentStatusHistories
                .Where(h => h.PaymentId == payment1Id ||
                            h.PaymentId == payment2Id)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentAttempts
                .Where(a => a.PaymentId == payment1Id ||
                            a.PaymentId == payment2Id)
                .ExecuteDeleteAsync();

            await cleanupDb.Payments
                .Where(p => p.PaymentBatchId == batchId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentBatches
                .Where(b => b.Id == batchId)
                .ExecuteDeleteAsync();

            await cleanupDb.BankAccounts
                .Where(a => a.Id == sourceBankAccountId)
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
    public async Task Bank_timeout_marks_payment_as_unknown_without_automatic_retry()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var sourceBankAccountId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var createdAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var bankGateway = new TimeoutBankPaymentGateway();

        // This test uses a dedicated database and runs sequentially.
        const string bic = "TIMEGB2LXXX";

        try
        {
            await using (var setupDb = CreateDbContext())
            {
                await setupDb.Database.MigrateAsync();

                setupDb.Tenants.Add(new Tenant(
                    tenantId,
                    "Timeout Test Tenant",
                    createdAtUtc));

                setupDb.LegalEntities.Add(new LegalEntity(
                    legalEntityId,
                    tenantId,
                    "Timeout Test Ltd",
                    $"IT-{Guid.NewGuid():N}",
                    "GB",
                    createdAtUtc));

                setupDb.Banks.Add(new Bank(
                    bankId,
                    "Timeout Test Bank",
                    bic,
                    createdAtUtc));

                setupDb.BankAccounts.Add(new BankAccount(
                    sourceBankAccountId,
                    legalEntityId,
                    bankId,
                    "GB82WEST12345698765432",
                    Currency.EUR,
                    createdAtUtc));

                setupDb.PaymentBatches.Add(new PaymentBatch(
                    batchId,
                    legalEntityId,
                    sourceBankAccountId,
                    Currency.EUR,
                    createdAtUtc));

                setupDb.Payments.Add(new Payment(
                    paymentId,
                    batchId,
                    125.50m,
                    "Timeout Test Supplier",
                    "GB29NWBK60161331926819",
                    "TIMEOUT-TEST-001",
                    createdAtUtc));

                await setupDb.SaveChangesAsync();
            }

            // Act: the bank throws a timeout.
            await using (var processingDb = CreateDbContext())
            {
                var service = new ProcessPaymentService(
                    new PaymentProcessingStore(processingDb, TimeProvider.System),
                    bankGateway,
                    TimeProvider.System,
                    NullLogger<ProcessPaymentService>.Instance);

                var processed = await service.ProcessNextAsync(
                    CancellationToken.None);

                Assert.That(processed, Is.True);
            }

            // Act: attempt another processing cycle.
            await using (var retryDb = CreateDbContext())
            {
                var service = new ProcessPaymentService(
                    new PaymentProcessingStore(retryDb, TimeProvider.System),
                    bankGateway,
                    TimeProvider.System,
                    NullLogger<ProcessPaymentService>.Instance);

                var processedAgain = await service.ProcessNextAsync(
                    CancellationToken.None);

                Assert.That(processedAgain, Is.False);
            }

            // Assert: verify persisted state using a fresh DbContext.
            await using (var verificationDb = CreateDbContext())
            {
                var batch = await verificationDb.PaymentBatches
                    .AsNoTracking()
                    .SingleAsync(b => b.Id == batchId);

                var payment = await verificationDb.Payments
                    .AsNoTracking()
                    .SingleAsync(p => p.Id == paymentId);

                var attempts = await verificationDb.PaymentAttempts
                    .AsNoTracking()
                    .Where(a => a.PaymentId == paymentId)
                    .ToListAsync();

                var history = await verificationDb.PaymentStatusHistories
                    .AsNoTracking()
                    .Where(h => h.PaymentId == paymentId)
                    .ToListAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        batch.Status,
                        Is.EqualTo(PaymentBatchStatus.Completed));

                    Assert.That(
                        payment.Status,
                        Is.EqualTo(PaymentStatus.Unknown));

                    Assert.That(
                        attempts,
                        Has.Count.EqualTo(1));

                    Assert.That(
                        attempts.Single().Outcome,
                        Is.EqualTo(PaymentAttemptOutcome.Unknown));

                    Assert.That(
                        history,
                        Has.Count.EqualTo(2));

                    Assert.That(
                        history.Any(h =>
                            h.FromStatus == PaymentStatus.Accepted &&
                            h.ToStatus == PaymentStatus.Processing),
                        Is.True);

                    Assert.That(
                        history.Any(h =>
                            h.FromStatus == PaymentStatus.Processing &&
                            h.ToStatus == PaymentStatus.Unknown),
                        Is.True);

                    Assert.That(
                        bankGateway.SubmissionCount,
                        Is.EqualTo(1),
                        "An unknown payment must not be submitted again.");
                });
            }
        }
        finally
        {
            // Remove only records belonging to this test.
            await using var cleanupDb = CreateDbContext();

            await cleanupDb.PaymentStatusHistories
                .Where(h => h.PaymentId == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentAttempts
                .Where(a => a.PaymentId == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.Payments
                .Where(p => p.Id == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentBatches
                .Where(b => b.Id == batchId)
                .ExecuteDeleteAsync();

            await cleanupDb.BankAccounts
                .Where(a => a.Id == sourceBankAccountId)
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
    public async Task Accepted_bank_response_without_reference_marks_payment_as_unknown()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var sourceBankAccountId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var createdAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var bankGateway = new MissingReferenceBankPaymentGateway();

        try
        {
            await using (var setupDb = CreateDbContext())
            {
                await setupDb.Database.MigrateAsync();

                setupDb.Tenants.Add(new Tenant(
                    tenantId,
                    "Missing Reference Test Tenant",
                    createdAtUtc));

                setupDb.LegalEntities.Add(new LegalEntity(
                    legalEntityId,
                    tenantId,
                    "Missing Reference Test Ltd",
                    $"IT-{Guid.NewGuid():N}",
                    "GB",
                    createdAtUtc));

                setupDb.Banks.Add(new Bank(
                    bankId,
                    "Missing Reference Test Bank",
                    "REFRGB2LXXX",
                    createdAtUtc));

                setupDb.BankAccounts.Add(new BankAccount(
                    sourceBankAccountId,
                    legalEntityId,
                    bankId,
                    "GB82WEST12345698765432",
                    Currency.EUR,
                    createdAtUtc));

                setupDb.PaymentBatches.Add(new PaymentBatch(
                    batchId,
                    legalEntityId,
                    sourceBankAccountId,
                    Currency.EUR,
                    createdAtUtc));

                setupDb.Payments.Add(new Payment(
                    paymentId,
                    batchId,
                    125.50m,
                    "Missing Reference Supplier",
                    "GB29NWBK60161331926819",
                    "MISSING-REFERENCE-001",
                    createdAtUtc));

                await setupDb.SaveChangesAsync();
            }

            // Act
            await using (var processingDb = CreateDbContext())
            {
                var service = new ProcessPaymentService(
                    new PaymentProcessingStore(processingDb, TimeProvider.System),
                    bankGateway,
                    TimeProvider.System,
                    NullLogger<ProcessPaymentService>.Instance);

                var processed = await service.ProcessNextAsync(
                    CancellationToken.None);

                Assert.That(processed, Is.True);
            }

            // Assert: verify persisted state using a fresh DbContext.
            await using (var verificationDb = CreateDbContext())
            {
                var batch = await verificationDb.PaymentBatches
                    .AsNoTracking()
                    .SingleAsync(b => b.Id == batchId);

                var payment = await verificationDb.Payments
                    .AsNoTracking()
                    .SingleAsync(p => p.Id == paymentId);

                var attempts = await verificationDb.PaymentAttempts
                    .AsNoTracking()
                    .Where(a => a.PaymentId == paymentId)
                    .ToListAsync();

                var history = await verificationDb.PaymentStatusHistories
                    .AsNoTracking()
                    .Where(h => h.PaymentId == paymentId)
                    .ToListAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        batch.Status,
                        Is.EqualTo(PaymentBatchStatus.Completed));

                    Assert.That(
                        payment.Status,
                        Is.EqualTo(PaymentStatus.Unknown));

                    Assert.That(
                        attempts,
                        Has.Count.EqualTo(1));

                    Assert.That(
                        attempts.Single().Outcome,
                        Is.EqualTo(PaymentAttemptOutcome.Unknown));

                    Assert.That(
                        history,
                        Has.Count.EqualTo(2));

                    Assert.That(
                        history.Any(h =>
                            h.FromStatus == PaymentStatus.Accepted &&
                            h.ToStatus == PaymentStatus.Processing),
                        Is.True);

                    Assert.That(
                        history.Any(h =>
                            h.FromStatus == PaymentStatus.Processing &&
                            h.ToStatus == PaymentStatus.Unknown),
                        Is.True);

                    Assert.That(
                        bankGateway.SubmissionCount,
                        Is.EqualTo(1));
                });
            }
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();

            await cleanupDb.PaymentStatusHistories
                .Where(h => h.PaymentId == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentAttempts
                .Where(a => a.PaymentId == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.Payments
                .Where(p => p.Id == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentBatches
                .Where(b => b.Id == batchId)
                .ExecuteDeleteAsync();

            await cleanupDb.BankAccounts
                .Where(a => a.Id == sourceBankAccountId)
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
    public async Task Accepted_bank_response_is_persisted_when_worker_is_cancelled()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var sourceBankAccountId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var createdAtUtc = DateTime.UtcNow.AddMinutes(-1);

        using var workerCancellation = new CancellationTokenSource();

        var bankGateway = new CancellingBankPaymentGateway(workerCancellation);

        try
        {
            await using (var setupDb = CreateDbContext())
            {
                await setupDb.Database.MigrateAsync();

                setupDb.Tenants.Add(new Tenant(
                    tenantId,
                    "Cancellation Test Tenant",
                    createdAtUtc));

                setupDb.LegalEntities.Add(new LegalEntity(
                    legalEntityId,
                    tenantId,
                    "Cancellation Test Ltd",
                    $"IT-{Guid.NewGuid():N}",
                    "GB",
                    createdAtUtc));

                setupDb.Banks.Add(new Bank(
                    bankId,
                    "Cancellation Test Bank",
                    "CANCGB2LXXX",
                    createdAtUtc));

                setupDb.BankAccounts.Add(new BankAccount(
                    sourceBankAccountId,
                    legalEntityId,
                    bankId,
                    "GB82WEST12345698765432",
                    Currency.EUR,
                    createdAtUtc));

                setupDb.PaymentBatches.Add(new PaymentBatch(
                    batchId,
                    legalEntityId,
                    sourceBankAccountId,
                    Currency.EUR,
                    createdAtUtc));

                setupDb.Payments.Add(new Payment(
                    paymentId,
                    batchId,
                    125.50m,
                    "Cancellation Test Supplier",
                    "GB29NWBK60161331926819",
                    "CANCELLATION-TEST-001",
                    createdAtUtc));

                await setupDb.SaveChangesAsync();
            }

            // Act: the bank accepts the payment while the worker is stopping.
            await using (var processingDb = CreateDbContext())
            {
                var service = new ProcessPaymentService(
                    new PaymentProcessingStore(processingDb, TimeProvider.System),
                    bankGateway,
                    TimeProvider.System,
                    NullLogger<ProcessPaymentService>.Instance);

                var processed = await service.ProcessNextAsync(
                    workerCancellation.Token);

                Assert.That(processed, Is.True);
                Assert.That(
                    workerCancellation.IsCancellationRequested,
                    Is.True);
            }

            // Assert: the accepted bank outcome was persisted.
            await using (var verificationDb = CreateDbContext())
            {
                var batch = await verificationDb.PaymentBatches
                    .AsNoTracking()
                    .SingleAsync(b => b.Id == batchId);

                var payment = await verificationDb.Payments
                    .AsNoTracking()
                    .SingleAsync(p => p.Id == paymentId);

                var attempt = await verificationDb.PaymentAttempts
                    .AsNoTracking()
                    .SingleAsync(a => a.PaymentId == paymentId);

                var history = await verificationDb.PaymentStatusHistories
                    .AsNoTracking()
                    .Where(h => h.PaymentId == paymentId)
                    .ToListAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        batch.Status,
                        Is.EqualTo(PaymentBatchStatus.Completed));

                    Assert.That(
                        payment.Status,
                        Is.EqualTo(PaymentStatus.Submitted));

                    Assert.That(
                        payment.BankReference,
                        Is.EqualTo($"TEST-{paymentId:N}"));

                    Assert.That(
                        attempt.Outcome,
                        Is.EqualTo(PaymentAttemptOutcome.Accepted));

                    Assert.That(
                        history,
                        Has.Count.EqualTo(2));

                    Assert.That(
                        history.Any(h =>
                            h.FromStatus == PaymentStatus.Processing &&
                            h.ToStatus == PaymentStatus.Submitted),
                        Is.True);

                    Assert.That(
                        bankGateway.SubmissionCount,
                        Is.EqualTo(1));
                });
            }
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();

            await cleanupDb.PaymentStatusHistories
                .Where(h => h.PaymentId == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentAttempts
                .Where(a => a.PaymentId == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.Payments
                .Where(p => p.Id == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentBatches
                .Where(b => b.Id == batchId)
                .ExecuteDeleteAsync();

            await cleanupDb.BankAccounts
                .Where(a => a.Id == sourceBankAccountId)
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
    public async Task Missing_bank_response_marks_payment_as_unknown()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var sourceBankAccountId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var createdAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var bankGateway = new NullResponseBankPaymentGateway();

        try
        {
            await using (var setupDb = CreateDbContext())
            {
                await setupDb.Database.MigrateAsync();

                setupDb.Tenants.Add(new Tenant(
                    tenantId,
                    "Null Response Test Tenant",
                    createdAtUtc));

                setupDb.LegalEntities.Add(new LegalEntity(
                    legalEntityId,
                    tenantId,
                    "Null Response Test Ltd",
                    $"IT-{Guid.NewGuid():N}",
                    "GB",
                    createdAtUtc));

                setupDb.Banks.Add(new Bank(
                    bankId,
                    "Null Response Test Bank",
                    "NULLGB2LXXX",
                    createdAtUtc));

                setupDb.BankAccounts.Add(new BankAccount(
                    sourceBankAccountId,
                    legalEntityId,
                    bankId,
                    "GB82WEST12345698765432",
                    Currency.EUR,
                    createdAtUtc));

                setupDb.PaymentBatches.Add(new PaymentBatch(
                    batchId,
                    legalEntityId,
                    sourceBankAccountId,
                    Currency.EUR,
                    createdAtUtc));

                setupDb.Payments.Add(new Payment(
                    paymentId,
                    batchId,
                    125.50m,
                    "Null Response Supplier",
                    "GB29NWBK60161331926819",
                    "NULL-RESPONSE-001",
                    createdAtUtc));

                await setupDb.SaveChangesAsync();
            }

            // Act: the bank adapter returns no submission result.
            await using (var processingDb = CreateDbContext())
            {
                var service = new ProcessPaymentService(
                    new PaymentProcessingStore(
                        processingDb,
                        TimeProvider.System),
                    bankGateway,
                    TimeProvider.System,
                    NullLogger<ProcessPaymentService>.Instance);

                var processed = await service.ProcessNextAsync(
                    CancellationToken.None);

                Assert.That(processed, Is.True);
            }

            // Act: verify that the unknown payment is not submitted again.
            await using (var retryDb = CreateDbContext())
            {
                var service = new ProcessPaymentService(
                    new PaymentProcessingStore(
                        retryDb,
                        TimeProvider.System),
                    bankGateway,
                    TimeProvider.System,
                    NullLogger<ProcessPaymentService>.Instance);

                var processedAgain = await service.ProcessNextAsync(
                    CancellationToken.None);

                Assert.That(processedAgain, Is.False);
            }

            // Assert: verify persisted state using a fresh DbContext.
            await using (var verificationDb = CreateDbContext())
            {
                var batch = await verificationDb.PaymentBatches
                    .AsNoTracking()
                    .SingleAsync(b => b.Id == batchId);

                var payment = await verificationDb.Payments
                    .AsNoTracking()
                    .SingleAsync(p => p.Id == paymentId);

                var attempts = await verificationDb.PaymentAttempts
                    .AsNoTracking()
                    .Where(a => a.PaymentId == paymentId)
                    .ToListAsync();

                var history = await verificationDb.PaymentStatusHistories
                    .AsNoTracking()
                    .Where(h => h.PaymentId == paymentId)
                    .ToListAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        batch.Status,
                        Is.EqualTo(PaymentBatchStatus.Completed));

                    Assert.That(
                        payment.Status,
                        Is.EqualTo(PaymentStatus.Unknown));

                    Assert.That(
                        attempts,
                        Has.Count.EqualTo(1));

                    Assert.That(
                        attempts.Single().Outcome,
                        Is.EqualTo(PaymentAttemptOutcome.Unknown));

                    Assert.That(
                        history,
                        Has.Count.EqualTo(2));

                    Assert.That(
                        history.Any(h =>
                            h.FromStatus == PaymentStatus.Accepted &&
                            h.ToStatus == PaymentStatus.Processing),
                        Is.True);

                    Assert.That(
                        history.Any(h =>
                            h.FromStatus == PaymentStatus.Processing &&
                            h.ToStatus == PaymentStatus.Unknown),
                        Is.True);

                    Assert.That(
                        bankGateway.SubmissionCount,
                        Is.EqualTo(1),
                        "An unknown payment must not be submitted again.");
                });
            }
        }
        finally
        {
            // Delete children before parents to respect foreign keys.
            await using var cleanupDb = CreateDbContext();

            await cleanupDb.PaymentStatusHistories
                .Where(h => h.PaymentId == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentAttempts
                .Where(a => a.PaymentId == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.Payments
                .Where(p => p.Id == paymentId)
                .ExecuteDeleteAsync();

            await cleanupDb.PaymentBatches
                .Where(b => b.Id == batchId)
                .ExecuteDeleteAsync();

            await cleanupDb.BankAccounts
                .Where(a => a.Id == sourceBankAccountId)
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

    private sealed class TimeoutBankPaymentGateway : IBankPaymentGateway
    {
        public int SubmissionCount { get; private set; }

        public Task<BankSubmissionResult> SubmitAsync(
            Guid paymentId,
            decimal amount,
            string beneficiaryAccountIdentifier,
            string reference,
            CancellationToken cancellationToken)
        {
            SubmissionCount++;

            throw new TimeoutException("The bank did not respond before the timeout.");
        }
    }

    private sealed class MissingReferenceBankPaymentGateway : IBankPaymentGateway
    {
        public int SubmissionCount { get; private set; }

        public Task<BankSubmissionResult> SubmitAsync(
            Guid paymentId,
            decimal amount,
            string beneficiaryAccountIdentifier,
            string reference,
            CancellationToken cancellationToken)
        {
            SubmissionCount++;

            return Task.FromResult(new BankSubmissionResult(
                BankSubmissionOutcome.Accepted,
                null,
                null));
        }
    }

    private sealed class CancellingBankPaymentGateway(CancellationTokenSource workerCancellation) : IBankPaymentGateway
    {
        public int SubmissionCount { get; private set; }

        public Task<BankSubmissionResult> SubmitAsync(
            Guid paymentId,
            decimal amount,
            string beneficiaryAccountIdentifier,
            string reference,
            CancellationToken cancellationToken)
        {
            SubmissionCount++;

            // Simulate shutdown after the bank has accepted the payment.
            workerCancellation.Cancel();

            return Task.FromResult(new BankSubmissionResult(
                BankSubmissionOutcome.Accepted,
                $"TEST-{paymentId:N}",
                null));
        }
    }

    private sealed class NullResponseBankPaymentGateway : IBankPaymentGateway
    {
        public int SubmissionCount { get; private set; }

        public Task<BankSubmissionResult> SubmitAsync(
            Guid paymentId,
            decimal amount,
            string beneficiaryAccountIdentifier,
            string reference,
            CancellationToken cancellationToken)
        {
            SubmissionCount++;

            return Task.FromResult<BankSubmissionResult>(null!);
        }
    }
}