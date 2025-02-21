using CryptoTradingSignal.Configuation;
using CryptoTradingSignal.Data;
using CryptoTradingSignal.ML;
using CryptoTradingSignal.Service;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ✅ Configure API & database
builder.Services.Configure<CoinMarketCapSettings>(
    builder.Configuration.GetSection("CoinMarketCap"));

// ✅ Register CoinMarketCapService with HttpClient
builder.Services.AddHttpClient<CoinMarketCapService>();

// ✅ Register MLModel as a Singleton (Persisted ML Model)
builder.Services.AddSingleton<MLModel>();

// ✅ Register Notification Service
builder.Services.AddSingleton<NotificationService>();

// ✅ Register SQLite Database Context (SCOPED)
builder.Services.AddDbContext<CryptoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Register CryptoSignalService as a Background Service
builder.Services.AddHostedService<CryptoSignalService>();

// ✅ Add Controllers
builder.Services.AddControllers();

// ✅ Register OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ✅ Enable OpenAPI/Swagger only in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ✅ Middleware
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// ✅ Ensure database is created on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CryptoDbContext>();
    dbContext.Database.EnsureCreated(); // Auto-creates the database if it doesn't exist
}

app.Run();
