using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Gateway.Controllers
{
    [ApiController]
    public class GatewayController : ControllerBase
    {
        // Allowed paths for different roles
        private static readonly string[] allowedUserPaths = { "/api/assets/user", "/api/notifications/user" };
        private static readonly string[] allowedAuditorPaths = { "/api/assets/public", "/api/notifications/public" };
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public GatewayController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [Route("{**catchAll}")]
        public async Task<IActionResult> HandleAllRequests()
        {
            try
            {
                var path = Request.Path.Value ?? "/";
                if (path == "/")
                {
                    return BadRequest(new
                    {
                        error = "Bad Request",
                        message = "Invalid request"
                    });
                }
                string? token = null;
                if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    var authValue = authHeader.ToString();
                    if (authValue.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
                    {
                        token = authValue.Substring("Bearer ".Length).Trim();
                    }
                }
                string? userName = null;
                if (!string.IsNullOrEmpty(token))
                {
                    userName = CheckUserRoleAndPath(token, path);
                }

                if (userName == "")
                {
                    return Unauthorized(new { error = "Invalid or inactive token" });
                }

                var method = Request.Method ?? "GET";
                var queryString = Request.QueryString.Value ?? string.Empty;
                var clientName = DetermineClientName(path ?? string.Empty);
                if (clientName == "DefaultServiceClient")
                {
                    return NotFound(new { error = "Resource not found", message = $"The requested resource '{path}' does not exist." });
                }
                var baseUrl = _configuration[$"ExternalServices:{clientName}:Url"] ?? string.Empty;
                var resourceInHost = $"{baseUrl}{path}{queryString}";
                string? requestBody = null;
                if (Request.ContentLength > 0 && Request.Body != null)
                {
                    using (var reader = new StreamReader(Request.Body))
                    {
                        requestBody = await reader.ReadToEndAsync();
                    }
                }

                // Forward request using the appropriate client
                var response = await ForwardRequest(clientName, resourceInHost, method, requestBody ?? string.Empty, userName ?? string.Empty);

                return response;
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Failed to forward request",
                    message = ex.Message
                });
            }
        }

        private string DetermineClientName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "DefaultServiceClient";
            var lowerPath = path.ToLowerInvariant();
            if (lowerPath.StartsWith("/api/assets")) return "AssetRegistryServiceClient";
            if (lowerPath.StartsWith("/api/user") || lowerPath.StartsWith("/api/roles")) return "IdentityServiceClient";
            if (lowerPath.StartsWith("/api/maintenanceschedule") || lowerPath.StartsWith("/api/warrantyinfo") || lowerPath.StartsWith("/api/maintenancehistory")) return "MaintainanceServiceClient";
            if (lowerPath.StartsWith("/api/notification")) return "NotificationServiceClient";
            return "DefaultServiceClient";
        }

        // private string? ValidateToken(string token, string path)
        // {
        //     string? userName = null;

            // var tokenClient = _httpClientFactory.CreateClient("TokenValidationServiceClient");
            // var organizationName = _configuration["AsgardeoSettings:OrganizationName"] ?? string.Empty;
            // var introspectUrl = $"https://api.asgardeo.io/t/{organizationName}/oauth2/introspect";
            // var request = new HttpRequestMessage(HttpMethod.Post, introspectUrl);
            // var body = $"token={token}";
            // request.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

            // var response = tokenClient.Send(request);
            // var responseBody = response.Content.ReadAsStringAsync().Result;
            // bool isActive = false;
            // try
            // {
            //     var json = System.Text.Json.JsonDocument.Parse(responseBody);
            //     if (json.RootElement.TryGetProperty("active", out var activeProp))
            //     {
            //         isActive = activeProp.GetBoolean();
            //     }
            //     if (json.RootElement.TryGetProperty("userName", out var userNameProp))
            //     {
            //         userName = userNameProp.GetString();
            //     }
            // }
            // catch
            // {
            //     // ignore parse errors, treat as inactive
            // }
            // return CheckUserRoleAndPath(token, path, userName ?? string.Empty);
        // }
        private string CheckUserRoleAndPath(string token, string path)
        {
        // Decode JWT and extract roles
            string[]? roles = null;
            string userName= "";
            string? assetPlatformRole = null;
            try
            {
                var parts = token.Split('.');
                if (parts.Length == 3)
                {
                    var payload = parts[1];
                    int mod4 = payload.Length % 4;
                    if (mod4 > 0) payload += new string('=', 4 - mod4);
                    var bytes = System.Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
                    var json = System.Text.Encoding.UTF8.GetString(bytes);
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("username", out var usernameProp)){
                        userName = usernameProp.GetString() ?? string.Empty;
                    }
                    if (doc.RootElement.TryGetProperty("roles", out var rolesProp))
                    {
                        if (rolesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            var list = new System.Collections.Generic.List<string>();
                            foreach (var r in rolesProp.EnumerateArray())
                            {
                                if (r.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var str = r.GetString();
                                    if (str != null)
                                        list.Add(str);
                                }
                            }
                            roles = list.ToArray();
                        }
                        else if (rolesProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var str = rolesProp.GetString();
                            roles = str != null ? new string[] { str } : System.Array.Empty<string>();
                        }
                    }
    
                    }
                if (roles != null)
                {
                    assetPlatformRole = System.Array.Find(roles, r => r != null && r.StartsWith("asset-platform"));
                                roles = roles.Where(r => r != null).ToArray();
                }
                if (!string.IsNullOrEmpty(assetPlatformRole))
                {
                    if (assetPlatformRole.Equals("asset-platform-admin", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return userName;
                    }
                }
                if (assetPlatformRole != null && assetPlatformRole.Equals("asset-platform-user", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (allowedUserPaths.Any(p => path != null && path.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        return userName;
                    }
                }
                if (assetPlatformRole != null && assetPlatformRole.Equals("asset-platform-auditor", System.StringComparison.OrdinalIgnoreCase))
                {
    
                    if (allowedAuditorPaths.Any(p => path != null && path.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)))
                    {                        
                        return userName;
                    }   
 
                    return userName ?? string.Empty;
                }
            }
            catch { /* ignore decode errors */ }
            
            return userName;
        }

        private async Task<IActionResult> ForwardRequest(string clientName, string targetUrl, string method, string body, string userName)
        {
            var httpClient = _httpClientFactory.CreateClient(clientName ?? "DefaultServiceClient");
            try
            {
                var request = new HttpRequestMessage(new HttpMethod(method ?? "GET"), targetUrl ?? string.Empty);

                // Add userIdentity header
                if (!string.IsNullOrEmpty(userName))
                {
                    request.Headers.Add("userIdentity", userName);
                }
                foreach (var header in Request.Headers)
                {
                    var headerName = header.Key.ToLower();
                    if (headerName != "host" &&
                        headerName != "connection" &&
                        headerName != "keep-alive" &&
                        headerName != "transfer-encoding" &&
                        headerName != "upgrade" &&
                        headerName != "content-length")
                    {
                        if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                        {
                            if (request.Content != null)
                            {
                                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                            }
                        }
                    }
                }

                // Add request body if present
                if (!string.IsNullOrEmpty(body))
                {
                    var contentType = Request.ContentType ?? "application/json";
                    request.Content = new StringContent(body, Encoding.UTF8, contentType);
                }
                else
                {
                    request.Content = null;
                }
                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = new ContentResult
                {
                    Content = responseBody,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };

                foreach (var header in response.Headers)
                {
                    if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) && 
                        !header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) &&
                        !header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase))
                    {
                        Response.Headers[header.Key] = header.Value.ToArray();
                    }
                }

                foreach (var header in response.Content.Headers)
                {
                    if (!header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) &&
                        !header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase))
                    {
                        Response.Headers[header.Key] = header.Value.ToArray();
                    }
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new
                {
                    error = "Bad Gateway",
                    message = $"External service ({clientName}) is unavailable",
                    details = ex.Message
                });
            }
        }
    }
}