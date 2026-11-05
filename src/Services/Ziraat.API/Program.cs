using PaymentSwitch.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5003");

var app = builder.Build();

app.MapPost("/api/pos/charge", async (PaymentRequest request, HttpContext context) =>
{
    await Task.Delay(90);
    var cardNetwork = context.Request.Headers["X-Card-Network"].FirstOrDefault() ?? "Ziraat Bankkart";
    var targetIban = request.MerchantTargetIban ?? "TR560004600123888000123456";

    decimal commissionRate = 0.0169m;
    decimal commission = Math.Round(request.Amount * commissionRate, 2);
    decimal netPayout = request.Amount - commission;

    return Results.Ok(new PaymentResponse(
        IsSuccess: true,
        TransactionId: $"TX-ZRT-{Guid.NewGuid().ToString("N")[..10].ToUpper()}",
        AcquirerBank: "Ziraat Bankasi Payten VPOS",
        CardType: cardNetwork,
        MaskedCardNumber: CardValidator.MaskPan(request.CardNumber),
        ProductPurchased: request.ProductName,
        AuthCode: $"ZRT-{Random.Shared.Next(100000, 999999)}",
        ProviderMode: "Ziraat Payten VPOS Engine",
        Settlement: new SettlementDetail("Akbank T.A.S.", targetIban, request.Amount, commissionRate, commission, netPayout, "SETTLED_VIA_EFT"),
        ProcessedAt: DateTime.UtcNow
    ));
});

app.MapGet("/health", () => Results.Ok(new { Service = "Ziraat VPOS", Status = "Healthy", Port = 5003 }));

app.Run();