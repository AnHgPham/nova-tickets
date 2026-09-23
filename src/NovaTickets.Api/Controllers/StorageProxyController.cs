using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace NovaTickets.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("manus-storage")]
public sealed class StorageProxyController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<StorageProxyController> logger) : ControllerBase
{
    [HttpGet("{**key}")]
    public async Task<IActionResult> Get(string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..", StringComparison.Ordinal)) return BadRequest("Invalid storage key.");
        var baseUrl = Environment.GetEnvironmentVariable("BUILT_IN_FORGE_API_URL") ?? configuration["BUILT_IN_FORGE_API_URL"];
        var apiKey = Environment.GetEnvironmentVariable("BUILT_IN_FORGE_API_KEY") ?? configuration["BUILT_IN_FORGE_API_KEY"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey)) return StatusCode(500, "Storage proxy not configured.");
        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/v1/storage/presign/get?path={Uri.EscapeDataString(key)}";
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) { logger.LogWarning("Storage presign failed with status {StatusCode} for {Key}", response.StatusCode, key); return StatusCode(502, "Storage backend error."); }
            var payload = await response.Content.ReadFromJsonAsync<StorageResponse>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(payload?.Url)) return StatusCode(502, "Storage backend returned no URL.");
            Response.Headers.CacheControl = "no-store";
            return Redirect(payload.Url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Storage proxy failed for {Key}", key);
            return StatusCode(502, "Storage proxy error.");
        }
    }

    private sealed record StorageResponse(string Url);
}
