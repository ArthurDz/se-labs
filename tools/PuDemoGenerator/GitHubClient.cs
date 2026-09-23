using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PuDemoGenerator;

/// <summary>
/// Минимальный клиент GitHub: REST для репозиториев и задач, GraphQL для досок (Projects v2).
/// Авторизация — только по токену (аналог PAT-авторизации в Azure DevOps Demo Generator).
/// </summary>
public sealed class GitHubClient : IDisposable
{
    private readonly HttpClient _http;

    public GitHubClient(string token)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PuDemoGenerator", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    // ---------- REST ----------

    public async Task<JsonElement> RestAsync(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubException($"{method} {path} → {(int)response.StatusCode} {response.ReasonPhrase}\n{text}");
        }

        return string.IsNullOrWhiteSpace(text)
            ? default
            : JsonDocument.Parse(text).RootElement.Clone();
    }

    public Task<JsonElement> GetAsync(string path) => RestAsync(HttpMethod.Get, path);

    public Task<JsonElement> PostAsync(string path, object body) => RestAsync(HttpMethod.Post, path, body);

    public Task<JsonElement> PatchAsync(string path, object body) => RestAsync(HttpMethod.Patch, path, body);

    /// <summary>Возвращает true, если запрос завершился успешно, и false при ошибке 404 или 422.</summary>
    public async Task<bool> TryPostAsync(string path, object body)
    {
        try
        {
            await PostAsync(path, body);
            return true;
        }
        catch (GitHubException)
        {
            return false;
        }
    }

    // ---------- GraphQL ----------

    public async Task<JsonElement> GraphQlAsync(string query, object? variables = null)
    {
        var payload = new { query, variables = variables ?? new { } };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("graphql", content);
        var text = await response.Content.ReadAsStringAsync();

        var root = JsonDocument.Parse(text).RootElement.Clone();
        if (root.TryGetProperty("errors", out var errors))
        {
            throw new GitHubException("GraphQL: " + errors.ToString());
        }

        return root.GetProperty("data");
    }

    public void Dispose() => _http.Dispose();
}

public sealed class GitHubException(string message) : Exception(message);
