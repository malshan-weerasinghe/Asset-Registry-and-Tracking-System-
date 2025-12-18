using AsgardeoMicroservice.Configuration;
using AsgardeoMicroservice.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AsgardeoMicroservice.Services
{
    public class AsgardeoService : IAsgardeoService
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenService _tokenService;
        private readonly AsgardeoSettings _settings;

        public AsgardeoService(HttpClient httpClient, ITokenService tokenService, IOptions<AsgardeoSettings> settings)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
            _settings = settings.Value;
        }

        public async Task SetAuthorizationHeaderAsync()
        {
            var token = await _tokenService.GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<string> GetUsersAsync(string? filter = null, string? attributes = null, int? startIndex = null, int? count = null)
        {
            await SetAuthorizationHeaderAsync();
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(filter))
                queryParams.Add($"filter={Uri.EscapeDataString(filter)}");
            if (!string.IsNullOrEmpty(attributes))
                queryParams.Add($"attributes={Uri.EscapeDataString(attributes)}");
            if (startIndex.HasValue)
                queryParams.Add($"startIndex={startIndex.Value}");
            if (count.HasValue)
                queryParams.Add($"count={count.Value}");
            var url = "scim2/Users";
            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetUserByIdAsync(string id, string? attributes = null)
        {
            await SetAuthorizationHeaderAsync();
            var url = $"scim2/Users/{id}";
            if (!string.IsNullOrEmpty(attributes))
                url += $"?attributes={Uri.EscapeDataString(attributes)}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> CreateUserAsync(JsonElement userRequest, string? attributes = null)
        {
            await SetAuthorizationHeaderAsync();
            var json = userRequest.GetRawText();
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var url = "scim2/Users";
            if (!string.IsNullOrEmpty(attributes))
                url += $"?attributes={Uri.EscapeDataString(attributes)}";
            var response = await _httpClient.PostAsync(url, stringContent);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Asgardeo API error: {response.StatusCode} - {errorContent}");
            }
            
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> UpdateUserAsync(string id, JsonElement userRequest, string? attributes = null)
        {
            await SetAuthorizationHeaderAsync();
            var json = userRequest.GetRawText();
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"scim2/Users/{id}";
            if (!string.IsNullOrEmpty(attributes))
                url += $"?attributes={Uri.EscapeDataString(attributes)}";
            var response = await _httpClient.PutAsync(url, stringContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PatchUserAsync(string id, JsonElement patchRequest, string? attributes = null)
        {
            await SetAuthorizationHeaderAsync();
            var json = patchRequest.GetRawText();
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"scim2/Users/{id}";
            if (!string.IsNullOrEmpty(attributes))
                url += $"?attributes={Uri.EscapeDataString(attributes)}";
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = stringContent };
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> DeleteUserAsync(string id, string? attributes = null)
        {
            await SetAuthorizationHeaderAsync();
            var url = $"scim2/Users/{id}";
            if (!string.IsNullOrEmpty(attributes))
                url += $"?attributes={Uri.EscapeDataString(attributes)}";
            var response = await _httpClient.DeleteAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> SearchUsersAsync(JsonElement searchRequest)
        {
            await SetAuthorizationHeaderAsync();
            var json = searchRequest.GetRawText();
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var url = "scim2/Users/.search";
            var response = await _httpClient.PostAsync(url, stringContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PatchUserRoleAsync(string roleId, JsonElement patchRequest)
        {
            await SetAuthorizationHeaderAsync();
            var json = patchRequest.GetRawText();
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"scim2/v3/Roles/{roleId}/Users";
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = stringContent };
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PatchAuditorRoleAsync(JsonElement patchRequest)
        {
            return await PatchUserRoleAsync(_settings.AuditorRoleId, patchRequest);
        }

        public async Task<string> PatchAuditUserRoleAsync(JsonElement patchRequest)
        {
            return await PatchUserAsync(_settings.UserRoleId, patchRequest);
        }
    }
}
