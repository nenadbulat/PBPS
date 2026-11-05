using Microsoft.EntityFrameworkCore;
using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Infrastructure.DB.Models;

namespace PaymentBatchProcessingService.Infrastructure.DB;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<PaymentBatch> PaymentBatches => Set<PaymentBatch>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<PaymentStatusHistory> PaymentStatusHistories => Set<PaymentStatusHistory>();
    public DbSet<BatchIdempotencyRecord> BatchIdempotencyRecords => Set<BatchIdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
    }
}

