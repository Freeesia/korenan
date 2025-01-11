using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using GoogleTrendsApi;
using korenan.ApiService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Session;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using NeoSmart.AsyncLock;
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

builder.AddRedisDistributedCache("cache");

builder.Services
    .AddHttpClient()
    .ConfigureHttpJsonOptions(op => op.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddSingleton<IWebSearchEngineConnector>(sp => new BingConnector(bingKey, sp.GetRequiredService<HttpClient>(), loggerFactory: sp.GetService<ILoggerFactory>()))
    //.AddSingleton<IWebSearchEngineConnector>(sp => new GoogleSearchConnector(googleKey, sp.GetRequiredService<ILogger<GoogleSearchConnector>>()))
    .AddSingleton(sp => KernelPluginFactory.CreateFromType<WebSearchEnginePlugin>("search", sp))
    .AddSingleton(sp => KernelPluginFactory.CreateFromType<TimePlugin>("time", serviceProvider: sp))
    .AddSingleton(sp => KernelPluginFactory.CreateFromType<WikipediaPlugin>("wiki", serviceProvider: sp))
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddSession(op =>
    {
        op.IdleTimeout = TimeSpan.FromDays(30);
        op.Cookie.HttpOnly = true;
        op.Cookie.IsEssential = true;
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.UseSession();

var sm = app.Services.GetRequiredService<ISessionStore>();

var game = new Game([], [], [], GameScene.WaitRoundStart, new());

var geminiSettings = new GeminiPromptExecutionSettings()
{
    SafetySettings =
    [
        new(GeminiSafetyCategory.Harassment, GeminiSafetyThreshold.BlockNone),
        new(GeminiSafetyCategory.DangerousContent, GeminiSafetyThreshold.BlockNone),
        new(new("HARM_CATEGORY_HATE_SPEECH"), GeminiSafetyThreshold.BlockNone),
    ],
};

var api = app.MapGroup("/api");

// プレイヤー・お題登録
api.MapPost("/regist", (HttpContext context, [FromBody] RegistRequest req) =>
{
    if (context.Session.Get<User>(nameof(User)) is not { } user)
    {
        user = new User(Guid.NewGuid(), req.Name);
        context.Session.Set(nameof(User), user);
    }
    if (game.Players.Any(p => p.Id == user.Id))
    {
        return Results.BadRequest("You have already registered.");
    }
    if (string.IsNullOrEmpty(req.Topic))
    {
        return Results.BadRequest("Topic is required.");
    }
    var player = new Player(user.Id, user.Name);
    game.Players.Add(player);
    game.Topics.Add(player.Id, req.Topic);
    return Results.Ok(user);
});

var roundLock = new AsyncLock();

// ラウンド開始
api.MapPost("/start", async ([FromServices] Kernel kernel) =>
{
    if (game.Players.Any(p => p.CurrentScene != GameScene.WaitRoundStart))
    {
        return Results.BadRequest("Some players are not ready.");
    }
    using var l = await roundLock.LockAsync();
    if (game.Rounds.LastOrDefault() is { LiarGuesses.Count: 0 })
    {
        return Results.BadRequest("直前のラウンドが終わっていません");
    }
    await StartNextRound(kernel);
    return Results.Ok();
});

api.MapPost("/next", async (HttpContext context, [FromServices] Kernel kernel) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    using var l = await roundLock.LockAsync();
    if (game.Rounds.LastOrDefault() is { Liars.Length: 0 })
    {
        return Results.BadRequest("直前のラウンドが終わっていません");
    }
    var player = game.Players.First(p => p.Id == user.Id);
    if (game.Topics.Count == game.Rounds.Count)
    {
        player.CurrentScene = GameScene.GameEnd;
        game = game with { CurrentScene = GameScene.GameEnd };
        return Results.Ok();
    }
    player.CurrentScene = GameScene.WaitRoundStart;
    if (game.Players.All(p => p.CurrentScene == GameScene.WaitRoundStart))
    {
        await StartNextRound(kernel);
    }
    return Results.Ok();
});

async Task StartNextRound(Kernel kernel)
{
    var topics = game.Topics.Values.Except(game.Rounds.Select(r => r.Topic)).ToArray();
    var topic = topics[Random.Shared.Next(topics.Length)];
    var topicInfo = string.Join(Environment.NewLine, [
        await kernel.InvokeAsync<string>("search", "Search", new() { ["query"] = topic }),
        await kernel.InvokeAsync<string>("wiki", "Search", new() { ["query"] = topic }),
        ]);
    var round = new Round(
        topic,
        topicInfo,
        game.Topics.Where(t => t.Value == topic).Select(t => t.Key).ToArray(),
        [],
        []);
    game.Rounds.Add(round);
    game = game with { CurrentScene = GameScene.QuestionAnswering };
}

// 質問と回答
api.MapPost("/question", async (HttpContext context, [FromServices] Kernel kernel, [FromBody] string input) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var round = game.Rounds.Last();
    if (round.Histories.Select(h => h.Result).OfType<QuestionResult>().Count(h => h.Player == user.Id) > game.Config.QuestionLimit)
    {
        throw new InvalidOperationException("You have reached the answer limit.");
    }
    var player = game.Players.First(p => p.Id == user.Id);
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
    var keywords = await kernel.GetRelationKeywords(round.Topic, input, geminiSettings);
    var prompt = new PromptTemplateConfig("""
        次の参考情報を基にして、ユーザーの質問に回答してください。

        ## 対象
        {{$topic}}

        ## 参考情報
        {{ $topicInfo }}
        {{ search $keywords }}

        ## ユーザーの質問
        {{ question intput=$intput target=$topic }}

        ## 回答の指針
        * 参考情報内に、質問に対して明確に肯定される内容があれば`yes`と回答してください。
        * 参考情報内に、質問に対して明確に否定される内容があれば`no`と回答してください。
        * 参考情報内に、質問に回答可能な情報が含まれていない場合は`no`と回答してください。
        * 対象がキャラクターかどうかをまず判断し、キャラクターの場合は、そのキャラクターに対する質問として回答してください。
          * 例: 対象が「ドラえもん」の場合、「ドラえもんは猫ですか？」と質問された場合は`no`と回答してください。
          * 例: 対象が「ドラえもん」の場合、「ドラえもんは猫型ロボットですか？」と質問された場合は`yes`と回答してください。
          * 例: 対象が「野比のび太」の場合、「野比のび太は人ですか？」と質問された場合は`yes`と回答してください。
          * 例: 対象が「野比のび太」の場合、「野比のび太は犬ですか？」と質問された場合は`no`と回答してください。
          * 例: 対象がバーチャルYouTuberの場合、「バーチャルYouTuberは人ですか？」と質問された場合は`yes`と回答してください。
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
        new() { ["topic"] = round.Topic, ["input"] = input, ["topicInfo"] = round.TopicInfo, ["keywords"] = keywords });

    var res = result.GetFromJson<QuestionResponse>();
    round.Histories.Add(new(new QuestionResult(player.Id, input, res.Result), res.Reason, result.RenderedPrompt ?? string.Empty));
    return res.Result;
});

api.MapGet("/round/{i}/history", ([FromRoute] int i) => game.Rounds[i].Histories.Select(h => h.Result));
api.MapGet("/round/{i}/history/internal", ([FromRoute] int i) => game.Rounds[i].Histories);

// ユーザーの解答と結果
api.MapPost("/answer", async (HttpContext context, [FromServices] Kernel kernel, [FromBody] string input) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var round = game.Rounds.Last();
    if (round.Histories.Select(h => h.Result).OfType<AnswerResult>().Count(h => h.Player == user.Id) > game.Config.AnswerLimit)
    {
        throw new InvalidOperationException("You have reached the answer limit.");
    }
    var player = game.Players.First(p => p.Id == user.Id);
    if (input == round.Topic)
    {
        round.Histories.Add(new(new AnswerResult(player.Id, input, AnswerResultType.Correct), "完全一致", string.Empty));
        player.Points += game.Config.CorrectPoint;
        game = game with { CurrentScene = GameScene.LiarGuess };
        return AnswerResultType.Correct;
    }
    var keywords = await kernel.GetRelationKeywords(round.Topic, input, geminiSettings);
    var prompt = new PromptTemplateConfig("""
        あなたはお題を当てるクイズの出題者であり、ユーザーの解答に対してお題と一致するかを判断する専門家です。
        参考情報を基にして、ユーザーの解答がお題と比較して直接的に同一存在かどうかを判断してください。

        ## お題
        {{ $correct }}

        ## ユーザーの解答
        {{ $answer }}

        ## お題に関する参考情報
        {{ $correctInfo }}

        ## 解答に関する参考情報
        {{ search $answer }}
        {{ wiki.Search $answer }}

        ## お題と解答の関係性に関する参考情報
        {{ search $keywords }}

        ## 判断および出力の指針
        1. 解答とお題が完全一致している場合は`correct`と出力してください。
        2. 解答とお題が完全一致していないが、表記揺れなど参考情報を元に解答がお題と必要十分かつ直接的に同一存在であると判断できる場合は`correct`と出力してください。
        3. 解答とお題が一致しないが、解答がお題の一部であり、解答がお題の十分条件を満たす場合は`correct`と出力してください。
        4. 解答とお題が一致しないが、お題が解答の一部であり、解答がお題の必要条件を満たすが、十分条件を満たさない場合は`more_specific`と出力してください。
        5. 上記のいずれにも当てはまらない場合は`incorrect`と出力してください。

        ## 出力の様式
        以下のJsonフォーマットにしたがって、判断を導き出した理由とともに判断結果を出力してください。
        {
            "reason": "判断の理由",
            "result": "`correct`、`more_specific`、`incorrect`のいずれかの判断結果"
        }
        """);
    prompt.AddExecutionSettings(geminiSettings);
    var result = await kernel.InvokeAsync(
        kernel.CreateFunctionFromPrompt(prompt),
        new() { ["correct"] = round.Topic, ["answer"] = input, ["correctInfo"] = round.TopicInfo, ["keywords"] = keywords });

    var res = result.GetFromJson<AnswerResponse>();
    round.Histories.Add(new(new AnswerResult(player.Id, input, res.Result), res.Reason, result.RenderedPrompt ?? string.Empty));
    if (res.Result == AnswerResultType.Correct)
    {
        player.Points += game.Config.CorrectPoint;
        game = game with { CurrentScene = GameScene.LiarGuess };
        return AnswerResultType.Correct;
    }
    // すべてのプレイヤーが解答制限に達した場合、ライアーを推理
    var playerAnswers = round.Histories.Select(h => h.Result).OfType<AnswerResult>().GroupBy(h => h.Player).ToDictionary(g => g.Key, g => g.Count());
    if (game.Players.Select(p => playerAnswers.TryGetValue(p.Id, out var count) ? count : 0).All(c => c >= game.Config.AnswerLimit))
    {
        var liars = game.Topics.Where(p => round.Topic == p.Value).Select(p => game.Players.First(pl => pl.Id == p.Key)).ToArray();
        foreach (var liar in liars)
        {
            liar.Points += game.Config.NoCorrectPoint;
        }
        game = game with { CurrentScene = GameScene.LiarGuess };
    }

    return res.Result;
});

// 嘘をついているプレイヤーの推理
api.MapPost("/guess", (HttpContext context, [FromBody] Guid target) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var round = game.Rounds.Last();
    round.LiarGuesses.Add(new(user.Id, target));
    if (round.LiarGuesses.Count != game.Players.Count)
    {
        return;
    }
    var liars = game.Topics.Where(p => round.Topic == p.Value).Select(p => p.Key).ToArray();
    foreach (var p in game.Players)
    {
        var liar = round.LiarGuesses.Find(t => t.Player == p.Id)!;
        if (liars.Contains(liar.Target))
        {
            p.Points += game.Config.CorrectPoint;
        }
    }
    game = game with { CurrentScene = GameScene.RoundSummary };
});

// シーン情報取得
api.MapGet("/scene", () => new CurrentScene(
    game.CurrentScene,
    game.Rounds.Count,
    game.Players.ToArray(),
    game.CurrentScene switch
    {
        GameScene.WaitRoundStart
            => new WaitRoundSceneInfo(
                game.Players.Where(p => p.CurrentScene == game.CurrentScene).Count()),
        GameScene.QuestionAnswering
            => new QuestionAnsweringSceneInfo(
                game.Rounds.Last().Histories.Select(h => h.Result).ToArray()),
        GameScene.LiarGuess
            => new LiarGuessSceneInfo(
                game.Rounds.Last().Topic,
                game.Rounds.Last()
                    .Histories
                    .Select(h => h.Result)
                    .OfType<AnswerResult>()
                    .Where(h => h.Result == AnswerResultType.Correct)
                    .Select(h => h.Player)
                    .ToArray(),
                [.. game.Rounds.Last().LiarGuesses]),
        GameScene.RoundSummary
            => new RoundSummaryInfo(
                game.Rounds.Last().Topic,
                game.Rounds.Last()
                    .GetCorrectPlayers()
                    .ToArray(),
                game.Rounds.Last()
                    .LiarGuesses
                    .Where(t => game.Rounds.Last().Liars.Contains(t.Target))
                    .Select(t => t.Player)
                    .ToArray()),
        GameScene.GameEnd
            => new GameEndInfo(
                game.Rounds.Select(r =>
                    new RoundResult(
                        r.Topic,
                        r.GetCorrectPlayers().ToArray(),
                        r.Liars,
                        r.LiarGuesses.Where(t => r.Liars.Contains(t.Target)).Select(t => t.Player).ToArray()))
                    .ToArray()),
        _ => throw new NotSupportedException(),
    }));

// プレイヤーのシーン情報更新
api.MapPost("/scene", (HttpContext context, [FromBody] GameScene scene) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var player = game.Players.First(p => p.Id == user.Id);
    player.CurrentScene = scene;
});

// プレイヤー情報取得
api.MapGet("/me", (HttpContext context) =>context.Session.Get<User>(nameof(User)) is { } u ? Results.Ok(u) : Results.NotFound());

// ゲームリセット
api.MapPost("/reset", () =>
{
    game = new Game([], [], [], GameScene.WaitRoundStart, new());
});

// 設定取得
api.MapGet("/config", () => game.Config);

// 設定更新
api.MapPost("/config", ([FromBody] Config newConfig) => game = game with { Config = newConfig });

#if DEBUG
api.MapGet("/wiki", ([FromServices] Kernel kernel, [FromQuery] string keyword) => kernel.InvokeAsync<string>("wiki", "Search", new() { ["query"] = keyword }));
api.MapGet("/search", ([FromServices] Kernel kernel, [FromQuery] string keyword) => kernel.InvokeAsync<string>("search", "Search", new() { ["query"] = keyword }));
api.MapGet("/trends/InterestOverTime", () => GoogleTrends.GetInterestOverTimeTyped([string.Empty], GeoId.Japan, DateOptions.LastMonth, GroupOptions.All, hl: "ja"));
api.MapGet("/trends/TrendingSearches", () => GoogleTrends.GetTrendingSearches("japan"));
api.MapGet("/trends/RealtimeSearches", () => GoogleTrends.GetRealtimeSearches("JP"));
api.MapGet("/trends/TopCharts", () => GoogleTrends.GetTopCharts(2020, hl: "ja", geo: "JP"));
api.MapGet("/trends/TodaySearches", () => GoogleTrends.GetTodaySearches(geo: "JP", hl: "ja"));
api.MapGet("/trends/RelatedQueries", () => GoogleTrends.GetRelatedQueries([string.Empty], geo: "JP"));
#endif


app.MapDefaultEndpoints();
app.MapFallbackToFile("/index.html");
app.Run();

record RegistRequest(string Name, string Topic);

record SemanticKernelOptions(string ModelId, string ApiKey, string BingKey, GoogleSearchParam GoogleSearch);
record QuestionResponse(string Reason, QuestionResultType Result);
record AnswerResponse(string Reason, AnswerResultType Result);

record CurrentScene(GameScene Scene, int Round, Player[] Players, ISceneInfo Info);

[JsonDerivedType(typeof(WaitRoundSceneInfo))]
[JsonDerivedType(typeof(QuestionAnsweringSceneInfo))]
[JsonDerivedType(typeof(LiarGuessSceneInfo))]
[JsonDerivedType(typeof(RoundSummaryInfo))]
[JsonDerivedType(typeof(GameEndInfo))]
interface ISceneInfo;

record WaitRoundSceneInfo(int Waiting) : ISceneInfo;
record QuestionAnsweringSceneInfo(IPlayerResult[] Histories) : ISceneInfo;
record LiarGuessSceneInfo(string Topic, Guid[] TopicCorrectPlayers, LiarGuess[] Targets) : ISceneInfo;
record RoundSummaryInfo(string Topic, Guid[] TopicCorrectPlayers, Guid[] LiarCorrectPlayers) : ISceneInfo;
record RoundResult(string Topic, Guid[] TopicCorrectPlayers, Guid[] LiarPlayers, Guid[] LiarCorrectPlayers);
record GameEndInfo(RoundResult[] Results) : ISceneInfo;

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
