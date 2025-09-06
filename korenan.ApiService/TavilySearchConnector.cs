using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Plugins.Web;

namespace Korenan.ApiService;

public class TavilySearchConnector : IWebSearchEngineConnector
{
    private readonly HttpClient httpClient;
    private readonly string apiKey;
    private readonly ILogger<TavilySearchConnector> logger;
    private readonly JsonSerializerOptions jsonOptions;

    public TavilySearchConnector(IOptions<TavilySearchParam> options, HttpClient httpClient, ILogger<TavilySearchConnector> logger)
    {
        this.httpClient = httpClient;
        this.apiKey = options.Value.ApiKey;
        this.logger = logger;
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
    }

    public async Task<IEnumerable<T>> SearchAsync<T>(string query, int count = 1, int offset = 0, CancellationToken cancellationToken = default)
    {
        var request = new TavilySearchRequest
        {
            ApiKey = this.apiKey,
            Query = query,
            SearchDepth = "advanced",
            IncludeAnswer = true,
            MaxResults = Math.Max(count, 5) // Tavilyは最低5件を推奨
        };

        var json = JsonSerializer.Serialize(request, jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("https://api.tavily.com/search", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<TavilySearchResponse>(responseContent, jsonOptions);

        var returnValues = new List<T>();

        // Answerが利用可能な場合は最初に追加
        if (!string.IsNullOrEmpty(result?.Answer) && typeof(T) == typeof(string))
        {
            returnValues.Add((T)(object)result.Answer);
        }

        // 検索結果からスニペットを追加
        if (result?.Results != null)
        {
            foreach (var item in result.Results.Take(count))
            {
                if (typeof(T) == typeof(string))
                {
                    var snippet = !string.IsNullOrEmpty(item.Content) ? item.Content : item.Title;
                    if (!string.IsNullOrEmpty(snippet))
                    {
                        returnValues.Add((T)(object)snippet);
                    }
                }
                else
                {
                    throw new NotSupportedException($"Type {typeof(T)} is not supported.");
                }
            }
        }

        return returnValues.Count <= count ? returnValues : returnValues.Take(count);
    }
}

public record TavilySearchParam()
{
    public required string ApiKey { get; init; }
}

internal record TavilySearchRequest
{
    public required string ApiKey { get; init; }
    public required string Query { get; init; }
    public string SearchDepth { get; init; } = "advanced";
    public bool IncludeAnswer { get; init; } = true;
    public int MaxResults { get; init; } = 5;
}

internal record TavilySearchResponse
{
    public string? Answer { get; init; }
    public List<TavilySearchResult>? Results { get; init; }
}

internal record TavilySearchResult
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}