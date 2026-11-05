namespace PaymentSwitch.Contracts;

public record PaymentRequest(
    string ProductName,
    string CardHolderName,
    string CardNumber,
    string ExpiryMonth,
    string ExpiryYear,
    string Cvv,
    decimal Amount,
    string Currency = "TRY",
    string? MerchantTargetIban = null
);

public record SettlementDetail(
    string BeneficiaryBank,
    string BeneficiaryIban,
    decimal GrossAmount,
    decimal PosCommissionRate,
    decimal CommissionDeducted,
    decimal NetSettledAmount,
    string SettlementStatus
);

public record PaymentResponse(
    bool IsSuccess,
    string TransactionId,
    string AcquirerBank,
    string CardType,
    string MaskedCardNumber,
    string ProductPurchased,
    string? AuthCode,
    string ProviderMode,
    SettlementDetail Settlement,
    DateTime ProcessedAt
);

public static class CardValidator
{
    public static bool IsValidLuhn(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber)) return false;

        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length is < 15 or > 16) return false;

        int sum = 0;
        bool shouldDouble = false;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int digit = digits[i] - '0';
            if (shouldDouble)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
            shouldDouble = !shouldDouble;
        }

        return sum % 10 == 0;
    }

    public static string MaskPan(string cardNumber)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length < 12) return "****************";
        return $"{digits[..6]}******{digits[^4..]}";
    }

    public static (string BankCode, string CardNetwork) ResolveBin(string cardNumber)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length < 6) return ("UNKNOWN", "Unknown");

        var bin = digits[..6];

        ReadOnlySpan<string> garantiVirtualBins = [
            "552879", "554960", "489455", "540668", "428220", "428221",
            "552608", "552609", "403280", "526955", "542119"
        ];

        ReadOnlySpan<string> garantiPhysicalBins = [
            "454360", "540667", "540061", "492193", "514915",
            "520019", "402275", "404308", "482489", "557829",
            "979206", "534261", "469884", "545616", "521824"
        ];

        ReadOnlySpan<string> akbankBins = ["454359", "552659", "557113", "402277", "435508", "521807"];
        ReadOnlySpan<string> ziraatBins = ["454671", "454672", "540130", "979200", "413226"];

        if (garantiVirtualBins.Contains(bin))
            return ("GARANTI", "Garanti BBVA Virtual Card");

        if (garantiPhysicalBins.Contains(bin))
            return ("GARANTI", "Garanti BBVA Physical Card");

        if (akbankBins.Contains(bin))
            return ("AKBANK", "Akbank Axess/Wings");

        if (ziraatBins.Contains(bin))
            return ("ZIRAAT", "Ziraat Bankkart");

        return ("GARANTI", $"Garanti BBVA Card (BIN: {bin})");
    }
}