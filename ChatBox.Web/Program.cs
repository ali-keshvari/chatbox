using Microsoft.EntityFrameworkCore;
using ChatBox.Web.Data;
using ChatBox.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure EF Core with SQL Server (SSMS)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=ChatBoxDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<ChatBoxDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 2. Register HTTP Client for Ollama
builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    var timeoutSeconds = builder.Configuration.GetValue<int?>("Ollama:TimeoutSeconds") ?? 180;
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});

// 3. Register Application Services
builder.Services.AddScoped<IFileExtractorService, FileExtractorService>();
builder.Services.AddScoped<IChatService, ChatService>();

// 4. Add MVC Controllers and Views
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();

// 5. Ensure SQL Server Database is Created automatically
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<ChatBoxDbContext>();
        db.Database.EnsureCreated();
        logger.LogInformation("دیتابیس ChatBoxDb با موفقیت در SQL Server مقداردهی اولیه شد.");
    }
    catch (Exception ex)
    {
        logger.LogWarning("امکان اتصال به SQL Server وجود ندارد یا دیتابیس در دسترس نیست. لطفا مطمئن شوید سرویس SQL Server در حال اجراست. خطا: {Message}", ex.Message);
    }
}

// 6. Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
