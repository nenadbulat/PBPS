using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PaymentBatchProcessingService.App.Exceptions;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.App.PaymentBatches;
using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.UnitTests.PaymentBatches;

[TestFixture]
public sealed class CreateBatchServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegalEntityA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LegalEntityB = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid AccountA = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid AccountB = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid BankId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ExistingBatchId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ExecuteAsync_ValidRequest_CreatesBatchAndPayments()
    {
        var store = StoreWithTenantA();
        var service = CreateService(store, TenantA);
        var request = ValidRequest() with { Payments = [ValidPayment(), ValidPayment() with { Reference = "INV-002" }] };

        var result = await service.ExecuteAsync(request, "batch-001", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.BatchId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(result.LegalEntityId, Is.EqualTo(LegalEntityA));
            Assert.That(result.Currency, Is.EqualTo(Currency.EUR));
            Assert.That(result.Status, Is.EqualTo(PaymentBatchStatus.Accepted));
            Assert.That(result.PaymentCount, Is.EqualTo(2));
            Assert.That(result.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
            Assert.That(store.CreateBatchCalls, Is.EqualTo(1));
            Assert.That(store.LastCreatedBatch?.Id, Is.EqualTo(result.BatchId));
            Assert.That(store.LastCreatedBatch?.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
            Assert.That(store.LastCreatedPayments, Has.Count.EqualTo(2));
            Assert.That(store.LastRequestFingerprint, Has.Length.EqualTo(64));
        });

        Assert.That(store.LastCreatedPayments, Is.Not.Null);
        foreach (var payment in store.LastCreatedPayments!)
        {
            Assert.Multiple(() =>
            {
                Assert.That(payment.PaymentBatchId, Is.EqualTo(result.BatchId));
                Assert.That(payment.Amount, Is.EqualTo(125.50m));
                Assert.That(payment.BeneficiaryName, Is.EqualTo("Example Supplier"));
                Assert.That(payment.BeneficiaryAccountIdentifier, Is.EqualTo("GB82WEST12345698765432"));
                Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Accepted));
                Assert.That(payment.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
            });
        }
    }

    [Test]
    public async Task ExecuteAsync_SameKeyAndRequest_ReplaysWithoutCreatingAgain()
    {
        var store = StoreWithTenantA();
        var service = CreateService(store, TenantA);
        var request = ValidRequest();

        var first = await service.ExecuteAsync(
            request,
            "batch-001",
            CancellationToken.None);

        var second = await service.ExecuteAsync(
            request,
            "batch-001",
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.IsReplay, Is.False);
            Assert.That(second.IsReplay, Is.True);
            Assert.That(second, Is.EqualTo(first with { IsReplay = true }));
            Assert.That(store.CreateBatchCalls, Is.EqualTo(1));
            Assert.That(store.GetBatchResultCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExecuteAsync_PreSeededMatchingIdempotencyRecord_ReturnsStoredResult()
    {
        var store = StoreWithTenantA();

        var original = await CreateService(store, TenantA)
            .ExecuteAsync(ValidRequest(), "original-key", CancellationToken.None);

        var fingerprint = store.LastRequestFingerprint!;
        var storedResult = original with { BatchId = ExistingBatchId };

        store.SeedIdempotencyRecord(
            TenantA,
            "seeded-key",
            ExistingBatchId,
            fingerprint,
            storedResult);

        var initialCalls = store.CreateBatchCalls;

        var result = await CreateService(store, TenantA)
            .ExecuteAsync(ValidRequest(), "seeded-key", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(storedResult with { IsReplay = true }));
            Assert.That(result.IsReplay, Is.True);
            Assert.That(store.CreateBatchCalls, Is.EqualTo(initialCalls));
            Assert.That(store.GetBatchResultCalls, Is.EqualTo(1));
        });
    }


    [Test]
    public async Task ExecuteAsync_IdempotencyRecordWithMissingBatch_Throws()
    {
        var source = StoreWithTenantA();
        await CreateService(source, TenantA).ExecuteAsync(ValidRequest(), "original", CancellationToken.None);
        var store = new FakePaymentBatchStore();
        store.SeedIdempotencyRecord(TenantA, "orphan", Guid.NewGuid(), source.LastRequestFingerprint!);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateService(store, TenantA).ExecuteAsync(ValidRequest(), "orphan", CancellationToken.None));
        Assert.That(store.CreateBatchCalls, Is.Zero);
    }

    [Test]
    public async Task ExecuteAsync_SameKeyWithDifferentRequest_ThrowsConflict()
    {
        var store = StoreWithTenantA();
        var service = CreateService(store, TenantA);
        await service.ExecuteAsync(ValidRequest(), "batch-001", CancellationToken.None);
        var changed = ValidRequest() with { Payments = [ValidPayment() with { Amount = 250m }] };

        Assert.ThrowsAsync<IdempotencyConflictException>(async () =>
            await service.ExecuteAsync(changed, "batch-001", CancellationToken.None));
        Assert.That(store.CreateBatchCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_ConcurrentWinnerWithSameFingerprint_ReturnsWinningBatch()
    {
        var store = StoreWithTenantA();

        var winner = await CreateService(store, TenantA)
            .ExecuteAsync(ValidRequest(), "winner", CancellationToken.None);

        var fingerprint = store.LastRequestFingerprint!;
        var initialCalls = store.CreateBatchCalls;

        store.CreateBatchOverride = (_, _, _, _, _) =>
            new BatchIdempotencyResult(winner.BatchId, fingerprint);

        var result = await CreateService(store, TenantA)
            .ExecuteAsync(ValidRequest(), "racing-key", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(winner with { IsReplay = true }));
            Assert.That(winner.IsReplay, Is.False);
            Assert.That(result.IsReplay, Is.True);
            Assert.That(store.CreateBatchCalls, Is.EqualTo(initialCalls + 1));
            Assert.That(store.GetBatchResultCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExecuteAsync_ConcurrentWinnerWithDifferentFingerprint_ThrowsConflict()
    {
        var store = StoreWithTenantA();
        var winner = await CreateService(store, TenantA).ExecuteAsync(ValidRequest(), "winner", CancellationToken.None);
        store.CreateBatchOverride = (_, _, _, _, _) =>
            new BatchIdempotencyResult(winner.BatchId, new string('A', 64));

        Assert.ThrowsAsync<IdempotencyConflictException>(async () =>
            await CreateService(store, TenantA).ExecuteAsync(ValidRequest(), "racing-key", CancellationToken.None));
        Assert.That(store.GetBatchResultCalls, Is.Zero);
    }

    [Test]
    public async Task ExecuteAsync_EquivalentNormalizedTextAndIban_ReturnsSameBatch()
    {
        var store = StoreWithTenantA();
        var service = CreateService(store, TenantA);
        var first = await service.ExecuteAsync(ValidRequest(), "batch-001", CancellationToken.None);
        var equivalent = ValidRequest() with
        {
            Payments =
            [
                ValidPayment() with
                {
                    BeneficiaryName = "  Example Supplier  ",
                    BeneficiaryAccountIdentifier = "GB82 WEST 1234 5698 7654 32",
                    Reference = "  INV-001  "
                }
            ]
        };

        var replay = await service.ExecuteAsync(equivalent, "batch-001", CancellationToken.None);
        Assert.That(replay.BatchId, Is.EqualTo(first.BatchId));
        Assert.That(store.CreateBatchCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_EquivalentDecimalScales_ProduceSameFingerprint()
    {
        var store = StoreWithTenantA();
        var service = CreateService(store, TenantA);
        var first = await service.ExecuteAsync(
            ValidRequest() with { Payments = [ValidPayment() with { Amount = 125.5m }] },
            "batch-001", CancellationToken.None);
        var replay = await service.ExecuteAsync(
            ValidRequest() with { Payments = [ValidPayment() with { Amount = 125.50m }] },
            "batch-001", CancellationToken.None);

        Assert.That(replay.BatchId, Is.EqualTo(first.BatchId));
        Assert.That(store.CreateBatchCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_SameKeyForDifferentTenants_CreatesSeparateBatchesInSameStore()
    {
        var store = StoreWithBothTenants();
        var first = await CreateService(store, TenantA).ExecuteAsync(ValidRequest(), "shared-key", CancellationToken.None);
        var second = await CreateService(store, TenantB).ExecuteAsync(
            ValidRequest() with { LegalEntityId = LegalEntityB, SourceBankAccountId = AccountB },
            "shared-key", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.BatchId, Is.Not.EqualTo(second.BatchId));
            Assert.That(store.CreateBatchCalls, Is.EqualTo(2));
            Assert.That(store.IdempotencyLookups, Does.Contain((TenantA, "shared-key")));
            Assert.That(store.IdempotencyLookups, Does.Contain((TenantB, "shared-key")));
        });
    }

    [Test]
    public void ExecuteAsync_OtherTenantsLegalEntity_ThrowsNotFound()
    {
        var store = StoreWithBothTenants();
        var request = ValidRequest() with { LegalEntityId = LegalEntityB, SourceBankAccountId = AccountB };
        Assert.ThrowsAsync<BatchNotFoundException>(async () =>
            await CreateService(store, TenantA).ExecuteAsync(request, "batch-001", CancellationToken.None));
        Assert.That(store.CreateBatchCalls, Is.Zero);
    }

    [Test]
    public void ExecuteAsync_MissingLegalEntity_ThrowsNotFound()
    {
        var store = new FakePaymentBatchStore();
        Assert.ThrowsAsync<BatchNotFoundException>(async () =>
            await CreateService(store, TenantA).ExecuteAsync(ValidRequest(), "batch-001", CancellationToken.None));
        Assert.Multiple(() =>
        {
            Assert.That(store.LastTenantId, Is.EqualTo(TenantA));
            Assert.That(store.LastLegalEntityId, Is.EqualTo(LegalEntityA));
            Assert.That(store.GetBankAccountCalls, Is.Zero);
            Assert.That(store.CreateBatchCalls, Is.Zero);
        });
    }

    [Test]
    public void ExecuteAsync_MissingBankAccount_ThrowsNotFound()
    {
        var store = new FakePaymentBatchStore();
        store.AddLegalEntity(ValidLegalEntity(TenantA, LegalEntityA));
        Assert.ThrowsAsync<BatchNotFoundException>(async () =>
            await CreateService(store, TenantA).ExecuteAsync(ValidRequest(), "batch-001", CancellationToken.None));
        Assert.That(store.GetBankAccountCalls, Is.EqualTo(1));
        Assert.That(store.CreateBatchCalls, Is.Zero);
    }

    [Test]
    public void ExecuteAsync_BankAccountBelongsToDifferentLegalEntity_ThrowsNotFound()
    {
        var store = new FakePaymentBatchStore();
        store.AddLegalEntity(ValidLegalEntity(TenantA, LegalEntityA));
        store.AddBankAccount(ValidBankAccount(AccountA, LegalEntityB));
        Assert.ThrowsAsync<BatchNotFoundException>(async () =>
            await CreateService(store, TenantA).ExecuteAsync(ValidRequest(), "batch-001", CancellationToken.None));
        Assert.That(store.CreateBatchCalls, Is.Zero);
    }

    [Test]
    public void ExecuteAsync_BankAccountCurrencyMismatch_ThrowsValidation()
    {
        var store = new FakePaymentBatchStore();
        store.AddLegalEntity(ValidLegalEntity(TenantA, LegalEntityA));
        store.AddBankAccount(ValidBankAccount(AccountA, LegalEntityA, Currency.USD));
        Assert.ThrowsAsync<BatchValidationException>(async () =>
            await CreateService(store, TenantA).ExecuteAsync(ValidRequest(), "batch-001", CancellationToken.None));
        Assert.That(store.CreateBatchCalls, Is.Zero);
    }

    [Test]
    public void ExecuteAsync_EmptyTenantId_ThrowsWithoutStoreCalls()
    {
        var store = StoreWithTenantA();
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateService(store, Guid.Empty).ExecuteAsync(ValidRequest(), "batch-001", CancellationToken.None));
        AssertNoStoreCalls(store);
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("   ")]
    public void ExecuteAsync_BlankIdempotencyKey_ThrowsWithoutStoreCalls(string key)
    {
        AssertInvalid(ValidRequest(), key);
    }

    [Test]
    public void ExecuteAsync_TooLongIdempotencyKey_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(ValidRequest(), new string('x', Constants.IdempotencyKeyLength + 1));
    }

    [Test]
    public void ExecuteAsync_EmptyLegalEntityId_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(ValidRequest() with { LegalEntityId = Guid.Empty });
    }

    [Test]
    public void ExecuteAsync_EmptySourceBankAccountId_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(ValidRequest() with { SourceBankAccountId = Guid.Empty });
    }

    [Test]
    public void ExecuteAsync_NullPayments_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(ValidRequest() with { Payments = null! });
    }

    [Test]
    public void ExecuteAsync_EmptyPayments_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(ValidRequest() with { Payments = [] });
    }

    [Test]
    public void ExecuteAsync_TooManyPayments_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(ValidRequest() with
        {
            Payments = Enumerable.Repeat(ValidPayment(), Constants.MaxPaymentsPerBatch + 1).ToArray()
        });
    }

    [Test]
    public void ExecuteAsync_NullPaymentItem_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(ValidRequest() with { Payments = [null!] });
    }

    [Test]
    public void ExecuteAsync_UndefinedCurrency_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(ValidRequest() with { Currency = (Currency)999 });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ExecuteAsync_NonPositiveAmount_ThrowsWithoutStoreCalls(int amount)
    {
        AssertInvalid(WithPayment(ValidPayment() with { Amount = amount }));
    }

    [Test]
    public void ExecuteAsync_ExcessiveDecimalPlaces_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(WithPayment(ValidPayment() with { Amount = 0.00001m }));
    }

    [Test]
    public void ExecuteAsync_AmountAboveMaximum_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(WithPayment(ValidPayment() with { Amount = Constants.MaxAmount + 1m }));
    }

    [Test]
    public void ExecuteAsync_BlankBeneficiaryName_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(WithPayment(ValidPayment() with { BeneficiaryName = "  " }));
    }

    [Test]
    public void ExecuteAsync_TooLongBeneficiaryName_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(WithPayment(ValidPayment() with
        {
            BeneficiaryName = new string('X', Constants.MaxNameLength + 1)
        }));
    }

    [Test]
    public void ExecuteAsync_InvalidBeneficiaryAccount_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(WithPayment(ValidPayment() with { BeneficiaryAccountIdentifier = "INVALID!" }));
    }

    [Test]
    public void ExecuteAsync_BlankReference_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(WithPayment(ValidPayment() with { Reference = " " }));
    }

    [Test]
    public void ExecuteAsync_TooLongReference_ThrowsWithoutStoreCalls()
    {
        AssertInvalid(WithPayment(ValidPayment() with
        {
            Reference = new string('R', Constants.MaxReferenceLength + 1)
        }));
    }

    private static void AssertInvalid(CreateBatchRequest request, string key = "batch-001")
    {
        var store = StoreWithTenantA();
        Assert.ThrowsAsync<BatchValidationException>(async () =>
            await CreateService(store, TenantA).ExecuteAsync(request, key, CancellationToken.None));
        AssertNoStoreCalls(store);
    }

    private static void AssertNoStoreCalls(FakePaymentBatchStore store)
    {
        Assert.Multiple(() =>
        {
            Assert.That(store.GetByIdempotencyKeyCalls, Is.Zero);
            Assert.That(store.GetLegalEntityCalls, Is.Zero);
            Assert.That(store.GetBankAccountCalls, Is.Zero);
            Assert.That(store.CreateBatchCalls, Is.Zero);
            Assert.That(store.GetBatchResultCalls, Is.Zero);
        });
    }

    private static CreateBatchRequest WithPayment(CreatePaymentRequest payment) =>
        ValidRequest() with { Payments = [payment] };

    private static CreateBatchRequest ValidRequest() =>
        new(LegalEntityA, AccountA, Currency.EUR, [ValidPayment()]);

    private static CreatePaymentRequest ValidPayment() =>
        new(125.50m, "Example Supplier", "GB82WEST12345698765432", "INV-001");

    private static LegalEntity ValidLegalEntity(Guid tenantId, Guid legalEntityId) =>
        new(legalEntityId, tenantId, "Example Payments Ltd", "REG-001", "GB", CreatedAtUtc);

    private static BankAccount ValidBankAccount(
        Guid accountId, Guid legalEntityId, Currency currency = Currency.EUR) =>
        new(accountId, legalEntityId, BankId, "GB82WEST12345698765432", currency, CreatedAtUtc);

    private static FakePaymentBatchStore StoreWithTenantA()
    {
        var store = new FakePaymentBatchStore();
        store.AddLegalEntity(ValidLegalEntity(TenantA, LegalEntityA));
        store.AddBankAccount(ValidBankAccount(AccountA, LegalEntityA));
        return store;
    }

    private static FakePaymentBatchStore StoreWithBothTenants()
    {
        var store = StoreWithTenantA();
        store.AddLegalEntity(ValidLegalEntity(TenantB, LegalEntityB));
        store.AddBankAccount(ValidBankAccount(AccountB, LegalEntityB));
        return store;
    }

    private static CreateBatchService CreateService(IPaymentBatchStore store, Guid tenantId) =>
        new(store, new FakeTenantContext(tenantId), new FixedTimeProvider(CreatedAtUtc), NullLogger<CreateBatchService>.Instance);

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId => tenantId;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakePaymentBatchStore : IPaymentBatchStore
    {
        private readonly Dictionary<Guid, LegalEntity> _legalEntities = [];
        private readonly Dictionary<Guid, BankAccount> _bankAccounts = [];
        private readonly Dictionary<(Guid TenantId, string Key), BatchIdempotencyResult> _idempotency = [];
        private readonly Dictionary<(Guid TenantId, Guid BatchId), CreateBatchResult> _results = [];

        public int GetByIdempotencyKeyCalls { get; private set; }
        public int GetLegalEntityCalls { get; private set; }
        public int GetBankAccountCalls { get; private set; }
        public int CreateBatchCalls { get; private set; }
        public int GetBatchResultCalls { get; private set; }
        public Guid? LastTenantId { get; private set; }
        public Guid? LastLegalEntityId { get; private set; }
        public PaymentBatch? LastCreatedBatch { get; private set; }
        public IReadOnlyCollection<Payment>? LastCreatedPayments { get; private set; }
        public string? LastRequestFingerprint { get; private set; }
        public List<(Guid TenantId, string Key)> IdempotencyLookups { get; } = [];

        public Func<PaymentBatch, IReadOnlyCollection<Payment>, Guid, string, string,
            BatchIdempotencyResult>? CreateBatchOverride
        { get; set; }

        public void AddLegalEntity(LegalEntity entity) => _legalEntities.Add(entity.Id, entity);
        public void AddBankAccount(BankAccount account) => _bankAccounts.Add(account.Id, account);

        public void SeedIdempotencyRecord(
            Guid tenantId, string key, Guid batchId, string fingerprint,
            CreateBatchResult? result = null)
        {
            _idempotency.Add((tenantId, key), new BatchIdempotencyResult(batchId, fingerprint));
            if (result is not null)
            {
                _results.Add((tenantId, batchId), result);
            }
        }

        public Task<LegalEntity?> GetLegalEntityAsync(
            Guid tenantId, Guid legalEntityId, CancellationToken cancellationToken)
        {
            GetLegalEntityCalls++;
            LastTenantId = tenantId;
            LastLegalEntityId = legalEntityId;
            _legalEntities.TryGetValue(legalEntityId, out var entity);
            return Task.FromResult(entity is not null && entity.TenantId == tenantId ? entity : null);
        }

        public Task<BankAccount?> GetBankAccountAsync(
            Guid legalEntityId, Guid bankAccountId, CancellationToken cancellationToken)
        {
            GetBankAccountCalls++;
            _bankAccounts.TryGetValue(bankAccountId, out var account);
            return Task.FromResult(account is not null && account.LegalEntityId == legalEntityId ? account : null);
        }

        public Task<BatchIdempotencyResult?> GetByIdempotencyKeyAsync(
            Guid tenantId, string idempotencyKey, CancellationToken cancellationToken)
        {
            GetByIdempotencyKeyCalls++;
            IdempotencyLookups.Add((tenantId, idempotencyKey));
            _idempotency.TryGetValue((tenantId, idempotencyKey), out var record);
            return Task.FromResult(record);
        }

        public Task<BatchIdempotencyResult> CreateBatchAsync(
            PaymentBatch batch, IReadOnlyCollection<Payment> payments, Guid tenantId,
            string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken)
        {
            CreateBatchCalls++;
            LastCreatedBatch = batch;
            LastCreatedPayments = payments.ToArray();
            LastRequestFingerprint = requestFingerprint;

            if (CreateBatchOverride is not null)
            {
                return Task.FromResult(CreateBatchOverride(
                    batch, payments, tenantId, idempotencyKey, requestFingerprint));
            }

            if (_idempotency.TryGetValue((tenantId, idempotencyKey), out var existing))
            {
                return Task.FromResult(existing);
            }

            var record = new BatchIdempotencyResult(batch.Id, requestFingerprint);
            _idempotency.Add((tenantId, idempotencyKey), record);
            _results.Add((tenantId, batch.Id), new CreateBatchResult(
                batch.Id, batch.LegalEntityId, batch.Currency, batch.Status,
                payments.Count, batch.CreatedAtUtc));
            return Task.FromResult(record);
        }

        public Task<CreateBatchResult?> GetBatchResultAsync(
            Guid tenantId, Guid batchId, CancellationToken cancellationToken)
        {
            GetBatchResultCalls++;
            _results.TryGetValue((tenantId, batchId), out var result);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<PaymentDetailsResult>?> GetBatchPaymentsAsync(
            Guid tenantId,
            Guid batchId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PaymentDetailsResult>?>(null);
        }
    }
}
