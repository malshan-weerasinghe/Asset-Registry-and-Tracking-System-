using Microsoft.AspNetCore.Authentication.JwtBearer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var organizationName = builder.Configuration["AsgardeoSettings:OrganizationName"];
        var clientId = builder.Configuration["AsgardeoSettings:ClientId"];
        var clientSecret = builder.Configuration["AsgardeoSettings:ClientSecret"];
        builder.Services.AddControllers();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MetadataAddress = $"https://api.asgardeo.io/t/{organizationName}/oauth2/token/.well-known/openid-configuration";

            options.Audience = $"{clientId}";

            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://api.asgardeo.io/t/{organizationName}/oauth2/token",

                ValidateAudience = true,
                ValidAudience = $"{clientId}",

                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };
        });

        builder.Services.AddAuthorization();
        builder.Services.AddHttpLogging(options =>
        {
            // Configure which fields to log (Headers, Body, etc.)
            options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;

            // Optional: Set limits for body logging to avoid performance issues
            options.RequestBodyLogLimit = 4096;
            options.ResponseBodyLogLimit = 4096;

            // Optional: Redact sensitive headers
            options.RequestHeaders.Add("Authorization");
            options.ResponseHeaders.Add("X-API-Key");
        });
        // Configure HttpClients as per appsettings.json
        builder.Services.AddHttpClient("AssetRegistryServiceClient", client =>
        {
            var url = builder.Configuration["ExternalServices:AssetRegistryServiceClient:Url"];
            client.BaseAddress = new Uri(url ?? "http://asset-registry-service:8081");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "ProxyAPI");
        });

        builder.Services.AddHttpClient("IdentitServiceClient", client =>
        {
            var url = builder.Configuration["ExternalServices:IdentityServiceClient:Url"];
            client.BaseAddress = new Uri(url ?? "http://audit-compliance-service:8082");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "ProxyAPI");
        });

        builder.Services.AddHttpClient("MaintainanceServiceClient", client =>
        {
            var url = builder.Configuration["ExternalServices:MaintainanceServiceClient:Url"];
            client.BaseAddress = new Uri(url ?? "http://maintainance-service:8083");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "ProxyAPI");
        });

        builder.Services.AddHttpClient("TokenValidationServiceClient", client =>
        {
            client.BaseAddress = new Uri($"https://api.asgardeo.io/t/{organizationName}/");
            client.Timeout = TimeSpan.FromSeconds(30);
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {authValue}");
        });

        builder.Services.AddHttpClient("NotificationServiceClient", client =>
        {
            var url = builder.Configuration["ExternalServices:NotificationServiceClient:Url"];
            client.BaseAddress = new Uri(url ?? "http://notification-service:8085");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "ProxyAPI");
        });

        var app = builder.Build();
        app.UseRouting();
        app.UseCors("AllowAll");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHttpLogging();
        app.MapControllers();
        app.Run();
    }
}
