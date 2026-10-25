using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace TimeBank.IntegrationTests.Infrastructure;

/// <summary>Small typed HTTP helper used by the integration tests.</summary>
public sealed class TimeBankApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    public TimeBankApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<HttpResponseMessage> GetAsync(string path, string? token = null) =>
        SendAsync(HttpMethod.Get, path, token);

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return _http.SendAsync(request);
    }

    public Task<HttpResponseMessage> SendJsonAsync<T>(
        HttpMethod method, string path, T body, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, body!.GetType(), Json),
            Encoding.UTF8,
            "application/json");

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return _http.SendAsync(request);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        return value ?? throw new InvalidOperationException(
            $"Response {response.StatusCode} did not contain a {typeof(T).Name} body.");
    }

    public static async Task<ProblemDetails?> ReadProblemAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }
}
