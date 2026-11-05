using Garanti.API.Services;
using PaymentSwitch.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5001");

builder.Services.AddHttpClient();
builder.Services.AddScoped<IyzicoGatewayService>();

var app = builder.Build();

app.MapPost("/api/pos/charge", async (
    PaymentRequest request,
    HttpContext context,
    IyzicoGatewayService gatewayService) =>
{
    try
    {
        var cardNetwork = context.Request.Headers["X-Card-Network"].FirstOrDefault() ?? "Garanti BBVA Card";
        var result = await gatewayService.ProcessChargeAndSettleAsync(request, cardNetwork);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.UnprocessableEntity(new { IsSuccess = false, Error = ex.Message });
    }
});

app.MapGet("/health", () => Results.Ok(new { Service = "Garanti BBVA VPOS", Status = "Healthy", Port = 5001 }));

app.Run();