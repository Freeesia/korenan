using System.Text.Json;
using System.Text.Json.Serialization;

namespace Korenan.ApiService;

/// <summary>
/// Tavily Search API のクライアント
/// </summary>
public sealed class TavilySearch(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// 検索してAIの回答と検索結果の本文を返す
    /// </summary>
    public async Task<string[]> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient(nameof(TavilySearch));
        using var response = await httpClient.PostAsJsonAsync("search", new SearchRequest(query), jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SearchResponse>(jsonOptions, cancellationToken);
        return [
            .. result?.Answer is { Length: > 0 } answer ? [answer] : Array.Empty<string>(),
            .. result?.Results.Select(r => r.Content) ?? [],
        ];
    }

    private sealed record SearchRequest(string Query, string SearchDepth = "advanced", bool IncludeAnswer = true, int MaxResults = 5);
    private sealed record SearchResponse(string? Answer, SearchResult[] Results);
    private sealed record SearchResult(string Title, string Url, string Content);
}
