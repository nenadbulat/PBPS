using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.Domain.Helpers;

public static class Guard
{
    public static string ValidateRequiredValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    public static string ValidateRequiredText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        var trimmedValue = value.Trim();

        if (trimmedValue.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmedValue;
    }

    public static void ValidateUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Date time must be in UTC.", nameof(value));
        }
    }

    public static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        var trimmedName = name.Trim();

        if (trimmedName.Length > Constants.MaxNameLength)
        {
            throw new ArgumentException($"Name cannot exceed {Constants.MaxNameLength} characters.", nameof(name));
        }

        return trimmedName;
    }

    public static string ValidateCountryCode(string countryCode)
    {
        //currently not validating if country exists, just validating the format of the country code
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            throw new ArgumentException("Country code is required.", nameof(countryCode));
        }

        var normalized = countryCode.Trim().ToUpperInvariant();

        if (normalized.Length != 2 || normalized.Any(c => c is < 'A' or > 'Z'))
        {
            throw new ArgumentException("Country code must contain exactly two letters.", nameof(countryCode));
        }

        return normalized;
    }

    public static string ValidateBic(string bic)
    {
        //validating just the structure of the BIC, not if it exists in the SWIFT registry
        if (string.IsNullOrWhiteSpace(bic))
        {
            throw new ArgumentException("BIC is required.", nameof(bic));
        }

        var normalized = bic.Trim().ToUpperInvariant();

        if (normalized.Length is not (8 or 11))
        {
            throw new ArgumentException("BIC must contain 8 or 11 characters.", nameof(bic));
        }

        // BIC format:
        // 4 letters: institution
        // 2 letters: country
        // 2 alphanumeric characters: location
        // 3 optional alphanumeric characters: branch

        bool IsLetter(char c) => c is >= 'A' and <= 'Z';
        bool IsAlphaNumeric(char c) => IsLetter(c) || c is >= '0' and <= '9';

        if (!normalized[..6].All(IsLetter) || !normalized[6..].All(IsAlphaNumeric))
        {
            throw new ArgumentException("BIC has an invalid format.", nameof(bic));
        }

        return normalized;
    }

    public static string ValidateAccountIdentifier(string accountIdentifier)
    {
        //validate IBAN structure, not control numbers or specific country length
        if (string.IsNullOrWhiteSpace(accountIdentifier))
        {
            throw new ArgumentException("Account identifier is required.", nameof(accountIdentifier));
        }

        // Accept spaces for readability and store the canonical form.
        var normalized = new string([.. accountIdentifier.Where(c => c != ' ')]).ToUpperInvariant();

        if (normalized.Length is < 15 or > Constants.MaxAccountIdentifierLength ||
            normalized.Length < 4 ||
            normalized[0] is < 'A' or > 'Z' ||
            normalized[1] is < 'A' or > 'Z' ||
            normalized[2] is < '0' or > '9' ||
            normalized[3] is < '0' or > '9' ||
            normalized.Any(c => !IsAsciiLetterOrDigit(c)))
        {
            throw new ArgumentException("Account identifier must have a valid IBAN structure.", nameof(accountIdentifier));
        }

        return normalized;
    }

    private static bool IsAsciiLetterOrDigit(char value) => value is >= 'A' and <= 'Z' or >= '0' and <= '9';

    public static void ValidateCurrency(Currency currency)
    {
        if (!Enum.IsDefined(currency))
        {
            throw new ArgumentOutOfRangeException(nameof(currency), "Invalid currency value.");
        }
    }

    public static void ValidateCompletionTime(DateTime completedAtUtc, DateTime createdAtUtc)
    {
        Guard.ValidateUtc(completedAtUtc);

        if (completedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("Completion time cannot precede creation time.", nameof(completedAtUtc));
        }
    }

    public static string? ValidateReason(string? reason)
    {
        if (reason is null)
        {
            return null;
        }

        var normalized = reason.Trim();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("Reason cannot be empty when provided.", nameof(reason));
        }

        if (normalized.Length > Constants.MaxReasonLength)
        {
            throw new ArgumentException($"Reason cannot exceed {Constants.MaxReasonLength} characters.", nameof(reason));
        }

        return normalized;
    }

    public static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Payment amount must be greater than zero.");
        }

        if (amount > Constants.MaxAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Payment amount must not exceed {Constants.MaxAmount}.");
        }

        if (decimal.Round(amount, Constants.AmountDecimalPlaces) != amount)
        {
            throw new ArgumentException($"Payment amount must have no more than {Constants.AmountDecimalPlaces} decimal places.", nameof(amount));
        }
    }
}
