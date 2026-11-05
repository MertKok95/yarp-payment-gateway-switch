using System.Threading.RateLimiting;
using PaymentGateway.Yarp.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 500,
                QueueLimit = 50,
                Window = TimeSpan.FromSeconds(1)
            }));
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();


// 1. 'X-Target-Bank' başlığını isteğe ekle
app.UseMiddleware<BinRoutingMiddleware>();

// 2. YARP yönlendiricisi, güncel başlığı bankaya yönlendirme
app.UseRouting();

// 3. Hız sınırı kontrolü
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { Gateway = "YARP Payment Switch", Status = "Healthy", Timestamp = DateTime.UtcNow }));
app.MapReverseProxy();

app.Run();