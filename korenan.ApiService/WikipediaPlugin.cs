using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Genbox.Wikipedia;
using Genbox.Wikipedia.Enums;
using Microsoft.SemanticKernel;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;

namespace Korenan.ApiService;

public sealed class WikipediaPlugin(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WikipediaClient client = new(httpClient)
    {
        DefaultLanguage = WikiLanguage.Japanese,
    };
    private readonly HttpClient httpClient = httpClient;

    [KernelFunction, Description("Search Wikipedia for a given query")]
    public async Task<string> SearchAsync([Description("Search query")] string query, CancellationToken cancellationToken = default)
    {
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

        var wikiRes = await this.httpClient.GetFromJsonAsync<WikiResponse>(
            $"https://ja.wikipedia.org/w/api.php?action=parse&formatversion=2&pageid={result.PageId}&format=json&redirects=1&prop=wikitext",
            jsonOptions,
            cancellationToken);
        var parseRes = wikiRes?.Parse ?? throw new InvalidOperationException();

        var parser = new WikitextParser();
        var doc = parser.Parse(parseRes.WikiText, cancellationToken);

        var sec = result.SectionTitle ?? parseRes.Redirects.FirstOrDefault()?.ToFragment;
        if (!string.IsNullOrEmpty(sec))
        {
            var secNode = doc.Lines.OfType<Heading>().FirstOrDefault(h => h.ToPlainText() == sec);
            var sectionText = new StringBuilder();
            var node = secNode?.NextNode;
            while (node is not Heading and not null)
            {
                sectionText.AppendLine(node.ToPlainText(NodePlainTextOptions.RemoveRefTags));
                node = node.NextNode;
            }
            return sectionText.ToString();
        }

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
