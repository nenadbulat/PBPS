namespace PaymentBatchProcessingService.Domain.Helpers;

public static class Constants
{
    public const int MaxNameLength = 200;
    public const int MaxAccountIdentifierLength = 34;
    public const int MaxReferenceLength = 140;
    public const int MaxBankReferenceLength = 200;
    public const int MaxReasonLength = 500;
    public const int MaxStatusLength = 20;
    public const int MaxCountryCodeLength = 2;
    public const int MaxCurrencyLength = 3;
    public const int BicLength = 11;
    public const int AmountDecimalPlaces = 4;
    public const int AmountPrecision = 19;
    public const decimal MaxAmount = 999999999999999.9999m;
    public const int IdempotencyKeyLength = 128;
    public const int RequestFingerprintLength = 64;
    public const int MaxPaymentsPerBatch = 1000;
}
