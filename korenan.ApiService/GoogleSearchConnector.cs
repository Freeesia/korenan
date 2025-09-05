using Google.Apis.CustomSearchAPI.v1;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Plugins.Web;

namespace Korenan.ApiService;

public class GoogleSearchConnector : IWebSearchEngineConnector
{
    private readonly CustomSearchAPIService client;
    private readonly string cx;
    private readonly ILogger<GoogleSearchConnector> logger;

    public GoogleSearchConnector(IOptions<GoogleSearchParam> options, ILogger<GoogleSearchConnector> logger)
    {
        this.client = new CustomSearchAPIService(new()
        {
            ApiKey = options.Value.ApiKey,
            ApplicationName = "korenan",
        });
        this.cx = options.Value.Cx;
        this.logger = logger;
    }

    public async Task<IEnumerable<T>> SearchAsync<T>(string query, int count = 1, int offset = 0, CancellationToken cancellationToken = default)
    {
        var req = this.client.Cse.List();
        req.Q = query;
        req.Hl = "ja";
        req.Cx = this.cx;
        req.Lr = "lang_ja";
        var res = await req.ExecuteAsync(cancellationToken).ConfigureAwait(false);


        var returnValues = new List<T>();

        foreach (var item in res.Items)
        {
            if (typeof(T) == typeof(string))
            {
                returnValues.Add((T)(object)item.Snippet);
            }
            else
            {
                throw new NotSupportedException($"Type {typeof(T)} is not supported.");
            }
        }

        return returnValues.Count <= count ? returnValues : returnValues.Take(count);
    }
}

public record GoogleSearchParam()
{
    public required string ApiKey { get; init; }
    public required string Cx { get; init; }
}
