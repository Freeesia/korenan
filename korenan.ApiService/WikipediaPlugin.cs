using System.ComponentModel;
using Genbox.Wikipedia;
using Genbox.Wikipedia.Enums;
using Microsoft.SemanticKernel;

namespace korenan.ApiService;

public sealed class WikipediaPlugin(HttpClient httpClient)
{
    private readonly WikipediaClient client = new(httpClient)
    {
        DefaultLanguage = WikiLanguage.Japanese,
    };

    [KernelFunction, Description("Search Wikipedia for a given query")]
    public async Task<string> SearchAsync([Description("Search query")] string query, CancellationToken cancellationToken = default)
    {
        var req = new WikiSearchRequest(query) { Limit = 1, IncludeInterWikiResults = true, EnableRewrites = true };
        var result = await client.SearchAsync(req);
        return result.QueryResult?.SearchResults.FirstOrDefault()?.Snippet ?? string.Empty;
    }
}
