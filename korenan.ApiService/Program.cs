using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using GoogleTrendsApi;
using korenan.ApiService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using GoogleTrends = GoogleTrendsApi.Api;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

var (modelId, apiKey, bingKey, _) = builder.Configuration.GetSection(nameof(SemanticKernelOptions)).Get<SemanticKernelOptions>()!;
var kernelBuikder = builder.Services.AddKernel()
    .AddGoogleAIGeminiChatCompletion(modelId, apiKey)
    .AddGoogleAIEmbeddingGeneration(modelId, apiKey);
builder.Services
    .AddHttpClient()
    .ConfigureHttpJsonOptions(op => op.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddSingleton<IWebSearchEngineConnector>(sp => new BingConnector(bingKey, sp.GetRequiredService<HttpClient>(), loggerFactory: sp.GetService<ILoggerFactory>()))
    //.AddSingleton<IWebSearchEngineConnector>(sp => new GoogleSearchConnector(googleKey, sp.GetRequiredService<ILogger<GoogleSearchConnector>>()))
    .AddSingleton(sp => KernelPluginFactory.CreateFromType<WebSearchEnginePlugin>("search", sp))
    .AddSingleton(sp => KernelPluginFactory.CreateFromType<TimePlugin>("time", serviceProvider: sp))
    .AddSingleton(sp => KernelPluginFactory.CreateFromType<WikipediaPlugin>("wiki", serviceProvider: sp));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};
var quiz = new Quiz();

var geminiSettings = new GeminiPromptExecutionSettings()
{
    SafetySettings =
    [
        new(GeminiSafetyCategory.Harassment, GeminiSafetyThreshold.BlockNone),
        new(GeminiSafetyCategory.DangerousContent, GeminiSafetyThreshold.BlockNone),
        new(new("HARM_CATEGORY_HATE_SPEECH"), GeminiSafetyThreshold.BlockNone),
    ],
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.MapGet("/wiki", ([FromServices] Kernel kernel, [FromQuery] string keyword) => kernel.InvokeAsync<string>("wiki", "Search", new() { ["query"] = keyword }));

app.MapGet("/search", ([FromServices] Kernel kernel, [FromQuery] string keyword) => kernel.InvokeAsync<string>("search", "Search", new() { ["query"] = keyword }));

app.MapPost("/target", async ([FromServices] Kernel kernel, [FromBody] string target) =>
{
    quiz.Correct = target;
    quiz.CorrectInfo = string.Join(Environment.NewLine, [
        await kernel.InvokeAsync<string>("search", "Search", new() { ["query"] = target }),
        await kernel.InvokeAsync<string>("wiki", "Search", new() { ["query"] = target })
        ]);
});

app.MapPost("/question", async ([FromServices] Kernel kernel, [FromBody] string input) =>
{
    var questionPrompt = new PromptTemplateConfig("""
        あなたは文章の校正を行うアシスタントです。
        与えられた対象と質問をつなげて、対象に対する質問文を作成してください。

        ## 対象
        {{$target}}

        ## 質問
        {{$input}}

        ### 例
        * 対象: 「東京」
        * 質問: 「首都ですか？」
        * 出力: 「東京は首都ですか？」

        * 対象: 「犬」
        * 質問: 「生き物？」
        * 出力: 「犬は生き物ですか？」

        * 対象: 「日本」
        * 質問: 「それは生き物ですか」
        * 出力: 「日本は生き物ですか？」
        """)
    {
        Name = "question",
        Description = "「対象」と「質問」から対象に対する質問文を生成する",
        InputVariables = [new() { Name = "input", IsRequired = true, Description = "質問" }, new() { Name = "target", IsRequired = true, Description = "対象" }],
    };
    questionPrompt.AddExecutionSettings(geminiSettings);
    var questionFunc = kernel.CreateFunctionFromPrompt(questionPrompt);
    kernel.ImportPluginFromFunctions("question", [questionFunc]);
    var keywords = await kernel.GetRelationKeywords(quiz.Correct, input, geminiSettings);
    var prompt = new PromptTemplateConfig("""
        次の参考情報を基にして、ユーザーの質問に回答してください。
        
        ## 参考情報
        {{ $correctInfo }}
        {{ search $keywords }}
              
        ## ユーザーの質問
        {{ question intput=$intput target=$correct }}
        
        ## 回答の指針
        * 参考情報内に、質問に対して明確に肯定される内容があれば`yes`と回答してください。
        * 参考情報内に、質問に対して明確に否定される内容があれば`no`と回答してください。
        * 参考情報内に、質問に回答可能な情報が含まれていない場合は`no`と回答してください。
        * 質問が「はい」「いいえ」で回答できない開いた質問の場合は`unanswerable`と回答してください。
        
        ## 出力の様式
        以下のJsonフォーマットにしたがって、質問に対して回答を導き出した理由とともに回答を出力してください。
        {
            "reason": "判断の理由",
            "result": "`yes`、`no`、`unanswerable`のいずれかの回答"
        }
        """);
    prompt.AddExecutionSettings(geminiSettings);
    var result = await kernel.InvokeAsync(
        kernel.CreateFunctionFromPrompt(prompt),
        new() { ["correct"] = quiz.Correct, ["input"] = input, ["correctInfo"] = quiz.CorrectInfo, ["keywords"] = keywords });

    var res = result.GetFromJson<QuestionResponse>();
    quiz.Histories.Add(new(new QuestionResult(input, res.Result), res.Reason, result.RenderedPrompt ?? string.Empty));
    return res.Result;
});

app.MapGet("/history", () => quiz.Histories.Select(h => h.Result));
app.MapGet("/history/internal", () => quiz.Histories);

app.MapPost("/answer", async ([FromServices] Kernel kernel, [FromBody] string input) =>
{
    if (input == quiz.Correct)
    {
        quiz.Histories.Add(new(new AnswerResult(input, AnswerResultType.Correct), "完全一致", string.Empty));
        return AnswerResultType.Correct;
    }
    var keywords = await kernel.GetRelationKeywords(quiz.Correct, input, geminiSettings);
    var prompt = new PromptTemplateConfig("""
        あなたはクイズの出題者であり、ユーザーからの回答の正誤を判断する専門家です。
        参考情報を基にして、ユーザーの回答がクイズの正解に対して同一かどうかを判断してください。

        ## 正解
        {{ $correct }}

        ## ユーザーの回答
        {{ $answer }}

        ## 正解に関する参考情報
        {{ $correctInfo }}

        ## 回答に関する参考情報
        {{ search $answer }}
        {{ wiki.Search $answer }}
        
        ## 正解と回答の関係性に関する参考情報
        {{ search $keywords }}

        ## 判断および出力の指針
        1. 正解と回答が完全一致している場合は`correct`と出力してください。
        2. 正解と回答が完全一致していないが、表記揺れなど参考情報を元に回答が正解と必要十分に同一であると判断できる場合は`correct`と出力してください。
        3. 正解と回答が一致しないが、回答が正解の一部であり、回答が正解の十分条件を満たす場合は`correct`と出力してください。
        4. 正解と回答が一致しないが、正解が回答の一部であり、回答が正解の必要条件を満たすが、十分条件を満たさない場合は`more_specific`と出力してください。
        5. 上記のいずれにも当てはまらない場合は`incorrect`と出力してください。

        ## 出力の様式
        以下のJsonフォーマットにしたがって、質問に対して回答を導き出した理由とともに回答を出力してください。
        {
            "reason": "判断の理由",
            "result": "`correct`、`more_specific`、`incorrect`のいずれかの回答"
        }
        """);
    prompt.AddExecutionSettings(geminiSettings);
    var result = await kernel.InvokeAsync(
        kernel.CreateFunctionFromPrompt(prompt),
        new() { ["correct"] = quiz.Correct, ["answer"] = input, ["correctInfo"] = quiz.CorrectInfo, ["keywords"] = keywords });

    var res = result.GetFromJson<AnswerResponse>();
    quiz.Histories.Add(new(new AnswerResult(input, res.Result), res.Reason, result.RenderedPrompt ?? string.Empty));
    return res.Result;
});

#if DEBUG
app.MapGet("/debug", () => new[]
{
    new HistoryInfo(new QuestionResult("東京", QuestionResultType.Yes), "東京は首都ですか？", "東京は日本の首都です。"),
    new HistoryInfo(new AnswerResult("東京", AnswerResultType.Correct), "完全一致", string.Empty),
    new HistoryInfo(new QuestionResult("犬", QuestionResultType.No), "犬は生き物ですか？", "犬は生き物です。"),
    new HistoryInfo(new AnswerResult("犬", AnswerResultType.Correct), "完全一致", string.Empty),
    new HistoryInfo(new QuestionResult("日本", QuestionResultType.Unanswerable), "日本は生き物ですか？", "日本は国です。"),
    new HistoryInfo(new AnswerResult("日本", AnswerResultType.Correct), "完全一致", string.Empty),
    new HistoryInfo(new QuestionResult("東京", QuestionResultType.Yes), "東京は首都ですか？", "東京は日本の首都です。"),
    new HistoryInfo(new AnswerResult("東京", AnswerResultType.Correct), "完全一致", string.Empty),
});
app.MapGet("/trends/InterestOverTime", () => GoogleTrends.GetInterestOverTimeTyped([string.Empty], GeoId.Japan, DateOptions.LastMonth, GroupOptions.All, hl: "ja"));
app.MapGet("/trends/TrendingSearches", () => GoogleTrends.GetTrendingSearches("japan"));
app.MapGet("/trends/RealtimeSearches", () => GoogleTrends.GetRealtimeSearches("JP"));
app.MapGet("/trends/TopCharts", () => GoogleTrends.GetTopCharts(2020, hl: "ja", geo: "JP"));
app.MapGet("/trends/TodaySearches", () => GoogleTrends.GetTodaySearches(geo: "JP", hl: "ja"));
app.MapGet("/trends/RelatedQueries", () => GoogleTrends.GetRelatedQueries([string.Empty], geo: "JP"));
#endif

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record SemanticKernelOptions(string ModelId, string ApiKey, string BingKey, GoogleSearchParam GoogleSearch);
record QuestionResponse(string Reason, QuestionResultType Result);
enum QuestionResultType { Yes, No, Unanswerable }
record AnswerResponse(string Reason, AnswerResultType Result);
enum AnswerResultType { Correct, MoreSpecific, Incorrect }
class Quiz
{
    public string Correct { get; set; }
    public string CorrectInfo { get; set; }
    public List<HistoryInfo> Histories { get; } = [];
}
record HistoryInfo(IResult Result, string Reason, string Prompt);
[JsonDerivedType(typeof(QuestionResult))]
[JsonDerivedType(typeof(AnswerResult))]
interface IResult;
record QuestionResult(string Question, QuestionResultType Result) : IResult;
record AnswerResult(string Answer, AnswerResultType Result) : IResult;

static class Extensions
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
        }
    };

    public static async Task<string> GetRelationKeywords(this Kernel kernel, string correct, string target, PromptExecutionSettings settings)
    {
        var ptompt = new PromptTemplateConfig("""
        対象の2つの単語、文章の関係性を検索エンジンで調査するためのキーワードを生成してください。

        ## 対象
        * {{$correct}}
        * {{$target}}

        ### 例
        * 対象: 「東京」「首都ですか？」
        * キーワード: 「東京 首都 かどうか」
        * 対象: 「犬」「生き物？」
        * キーワード: 「犬 生き物 かどうか」
        * 対象: 「日本」「生き物」
        * キーワード: 「日本 生き物 かどうか」

        キーワードはスペース区切りで質問に対する回答を得ることができるような検索エンジンへ渡す情報を出力してください。
        キーワード以外の情報は出力しないでください。
        """)
        {
            Name = "keywords",
            InputVariables = [new() { Name = "correct" }, new() { Name = "target" }],
        };
        ptompt.AddExecutionSettings(settings);
        var keywordsFunc = kernel.CreateFunctionFromPrompt(ptompt);
        return await keywordsFunc.InvokeAsync<string>(kernel, new() { ["correct"] = correct, ["target"] = target }) ?? string.Empty;
    }

    public static T GetFromJson<T>(this FunctionResult result)
    {
        var json = result.GetValue<string>()!.Trim('`', '\n');
        if (!json.StartsWith('{'))
        {
            json = json[json.IndexOf('{')..];
        }
        return JsonSerializer.Deserialize<T>(json, jsonOptions)!;
    }
}