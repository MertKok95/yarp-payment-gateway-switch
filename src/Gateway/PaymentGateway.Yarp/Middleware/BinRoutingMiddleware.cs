using System.Collections.Concurrent;
using System.Text.Json;
using PaymentSwitch.Contracts;

namespace PaymentGateway.Yarp.Middleware;

public sealed class BinRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BinRoutingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, string> IdempotencyStore = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public BinRoutingMiddleware(RequestDelegate next, ILogger<BinRoutingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/pos/charge") && HttpMethods.IsPost(context.Request.Method))
        {
            var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(idempotencyKey) && IdempotencyStore.TryGetValue(idempotencyKey, out var existingTx))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new
                {
                    ErrorCode = "DUPLICATE_REQUEST",
                    Message = $"Transaction with Idempotency-Key '{idempotencyKey}' has already been routed to {existingTx}."
                });
                return;
            }

            context.Request.EnableBuffering();

            var payload = await JsonSerializer.DeserializeAsync<PaymentRequest>(context.Request.Body, JsonOptions);
            context.Request.Body.Position = 0;

            if (payload is null || payload.Amount <= 0)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { ErrorCode = "INVALID_PAYLOAD", Message = "Amount must be greater than zero." });
                return;
            }

            if (!CardValidator.IsValidLuhn(payload.CardNumber))
            {
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsJsonAsync(new
                {
                    ErrorCode = "LUHN_VALIDATION_FAILED",
                    MaskedPan = CardValidator.MaskPan(payload.CardNumber),
                    Message = "Card number failed Luhn checksum validation."
                });
                return;
            }

            var (bankCode, cardNetwork) = CardValidator.ResolveBin(payload.CardNumber);

            context.Request.Headers["X-Target-Bank"] = bankCode;
            context.Request.Headers["X-Card-Network"] = cardNetwork;

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                IdempotencyStore.TryAdd(idempotencyKey, bankCode);
            }

            _logger.LogInformation(
                "Switch Routing | PAN: {MaskedPan} | Network: {Network} | Amount: {Amount} {Currency} -> Cluster: {BankCode}",
                CardValidator.MaskPan(payload.CardNumber),
                cardNetwork,
                payload.Amount,
                payload.Currency,
                bankCode);
        }

        await _next(context);
    }
}