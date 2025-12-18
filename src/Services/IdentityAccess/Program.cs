using AsgardeoMicroservice.Services;
using AsgardeoMicroservice.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Asgardeo settings
builder.Services.Configure<AsgardeoSettings>(builder.Configuration.GetSection("AsgardeoSettings"));

// Register HTTP client
builder.Services.AddHttpClient<IAsgardeoService, AsgardeoService>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<AsgardeoSettings>>().Value;
    client.BaseAddress = new Uri($"https://api.asgardeo.io/t/{settings.OrganizationName}/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Register UserService HTTP client
builder.Services.AddHttpClient<IMeService, MeService>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<AsgardeoSettings>>().Value;
    client.BaseAddress = new Uri($"https://api.asgardeo.io/t/{settings.OrganizationName}/");
    client.DefaultRequestHeaders.Add("Accept", "application/scim+json");
});

// Register services
// Removed redundant AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable CORS
app.UseCors("AllowAll");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseSwagger();
app.UseSwaggerUI();
app.Run();
