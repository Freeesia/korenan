using System.Text.Json;
using Genbox.Wikipedia;
using Genbox.Wikipedia.Enums;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;

namespace Korenan.ApiService;

/// <summary>
/// 日本語版 Wikipedia から記事本文を取得する
/// </summary>
public sealed class WikipediaSearch(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient(nameof(WikipediaSearch));
        using var client = new WikipediaClient(httpClient)
        {
            DefaultLanguage = WikiLanguage.Japanese,
        };
        var req = new WikiSearchRequest(query)
        {
            Limit = 1,
            IncludeInterWikiResults = true,
            EnableRewrites = true,
            WhatToSearch = WikiWhat.Text,
            PropertiesToInclude = WikiProperty.SectionTitle,
        };
        var res = await client.SearchAsync(req, cancellationToken);
        if (res.QueryResult?.SearchResults is not [var result])
        {
            return string.Empty;
        }

        var wikiRes = await httpClient.GetFromJsonAsync<WikiResponse>(
            $"https://ja.wikipedia.org/w/api.php?action=parse&formatversion=2&pageid={result.PageId}&format=json&redirects=1&prop=wikitext",
            jsonOptions,
            cancellationToken);
        var parseRes = wikiRes?.Parse ?? throw new InvalidOperationException();

        var parser = new WikitextParser();
        var doc = parser.Parse(parseRes.WikiText, cancellationToken);

        // 関連項目、脚注以降はあまり重要ではない情報なので削る
        Node? delete = doc.Lines.OfType<Heading>().FirstOrDefault(h => h.ToPlainText() is "関連項目" or "脚注");
        while (delete is not null)
        {
            var temp = delete.NextNode;
            delete.Remove();
            delete = temp;
        }

        return doc.ToPlainText(NodePlainTextOptions.RemoveRefTags);
    }

    private record WikiResponse(Parse? Parse);
    private record Parse(string Title, string WikiText, Redirect[] Redirects);
    private record Redirect(string From, string To, string? ToFragment);
}
