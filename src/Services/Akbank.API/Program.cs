using System.Collections.Concurrent;
using PaymentSwitch.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5002");

var app = builder.Build();

// Satıcının Akbank IBAN hesap hareketlerini ve toplam bakiyesini tutan defter
var ibanLedger = new ConcurrentDictionary<string, decimal>();
var settlementLogs = new ConcurrentQueue<object>();

// 1. Garanti POS'tan veya diğer bankalardan gelen hakediş (Payout) ödemelerini Akbank IBAN'ına yatıran uç nokta
app.MapPost("/api/settlement/credit", (SettlementTransferRequest transfer) =>
{
    var updatedBalance = ibanLedger.AddOrUpdate(
        transfer.TargetIban,
        transfer.NetAmount,
        (_, current) => current + transfer.NetAmount);

    var entry = new
    {
        TransferId = $"AKB-STL-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
        FromBank = transfer.SourceBank,
        CreditedIban = transfer.TargetIban,
        AmountCredited = transfer.NetAmount,
        NewAccountBalance = updatedBalance,
        SourceTransactionId = transfer.OriginalTransactionId,
        Timestamp = DateTime.UtcNow
    };

    settlementLogs.Enqueue(entry);
    app.Logger.LogInformation(
        "[AKBANK IBAN CREDIT] +{Amount} TRY credited to {Iban} from {SourceBank}. New Balance: {Balance} TRY",
        transfer.NetAmount, transfer.TargetIban, transfer.SourceBank, updatedBalance);

    return Results.Ok(entry);
});

// 2. Akbank IBAN Bakiyesini ve Hesap Hareketlerini Sorgulama Uç Noktası
app.MapGet("/api/settlement/balance/{iban}", (string iban) =>
{
    ibanLedger.TryGetValue(iban, out var balance);
    return Results.Ok(new
    {
        Bank = "Akbank T.A.S.",
        Iban = iban,
        AvailableBalance = balance,
        Currency = "TRY",
        RecentTransfers = settlementLogs.ToArray()
    });
});

// 3. Müşteri doğrudan Akbank kartıyla alışveriş yaparsa çalışan POS uç noktası
app.MapPost("/api/pos/charge", async (PaymentRequest request, HttpContext context) =>
{
    await Task.Delay(75);
    var cardNetwork = context.Request.Headers["X-Card-Network"].FirstOrDefault() ?? "Akbank Axess";
    var targetIban = request.MerchantTargetIban ?? "TR560004600123888000123456";

    decimal commissionRate = 0.0199m;
    decimal commission = Math.Round(request.Amount * commissionRate, 2);
    decimal netPayout = request.Amount - commission;

    ibanLedger.AddOrUpdate(targetIban, netPayout, (_, current) => current + netPayout);

    return Results.Ok(new PaymentResponse(
        IsSuccess: true,
        TransactionId: $"TX-AKB-{Guid.NewGuid().ToString("N")[..10].ToUpper()}",
        AcquirerBank: "Akbank Nestpay VPOS",
        CardType: cardNetwork,
        MaskedCardNumber: CardValidator.MaskPan(request.CardNumber),
        ProductPurchased: request.ProductName,
        AuthCode: $"AKB-{Random.Shared.Next(100000, 999999)}",
        ProviderMode: "Akbank Direct Internal POS",
        Settlement: new SettlementDetail("Akbank T.A.S.", targetIban, request.Amount, commissionRate, commission, netPayout, "INSTANT_INTERNAL_SETTLEMENT"),
        ProcessedAt: DateTime.UtcNow
    ));
});

app.MapGet("/health", () => Results.Ok(new { Service = "Akbank VPOS & Settlement Ledger", Status = "Healthy", Port = 5002 }));

app.Run();

public record SettlementTransferRequest(string SourceBank, string TargetIban, decimal NetAmount, string OriginalTransactionId);