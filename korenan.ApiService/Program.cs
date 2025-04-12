using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.HighPerformance;
using GoogleTrendsApi;
using Korenan.ApiService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
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
    .AddSingleton(sp => sp.GetRequiredService<IDistributedCache>() as IBufferDistributedCache ?? throw new InvalidOperationException("内部的にはIBufferDistributedCacheで実装されているはず"))
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

static async Task<Game?> GetCurrentGame(HttpContext context, IBufferDistributedCache cache)
{
    if (context.Session.Get<User>(nameof(User)) is not { } user)
    {
        return null;
    }
    var room = await cache.GetStringAsync($"user/{user.Id}/room", context.RequestAborted);
    if (string.IsNullOrEmpty(room))
    {
        return null;
    }
    return await cache.Get<Game>($"game/room/{room}", context.RequestAborted);
}
static async Task<Game?> GetGameFromUser(User user, IBufferDistributedCache cache, CancellationToken token = default)
{
    var room = await cache.GetStringAsync($"user/{user.Id}/room", token);
    if (string.IsNullOrEmpty(room))
    {
        return null;
    }
    return await cache.Get<Game>($"game/room/{room}", token);
}
static async Task NextScene(IBufferDistributedCache cache, Game game, Kernel kernel, CancellationToken token = default)
{
    switch (game.CurrentScene)
    {
        case GameScene.WaitRoundStart:
        case GameScene.TopicSelecting:
            break;
        case GameScene.QuestionAnswering:
            {
                var round = game.Rounds.Last();
                // すべてのプレイヤーが解答制限に達した場合、ライアーを推理
                var playerAnswers = round.Histories.Select(h => h.Result).OfType<AnswerResult>().GroupBy(h => h.Player).ToDictionary(g => g.Key, g => g.Count());
                if (game.Players.Select(p => playerAnswers.TryGetValue(p.Id, out var count) ? count : 0).Any(c => c < game.Config.AnswerLimit))
                {
                    break;
                }
                await cache.Update<Game>(
                    $"game/room/{game.Id}",
                    g =>
                    {
                        var liars = g.Topics.Where(p => round.Topic == p.Value).Select(p => g.Players.First(pl => pl.Id == p.Key)).ToArray();
                        foreach (var liar in liars)
                        {
                            liar.Points += g.Config.NoCorrectPoint;
                        }
                        return g with
                        {
                            Players = [.. g.Players.Select(p => p with { CurrentScene = GameScene.LiarGuess })],
                            CurrentScene = GameScene.LiarGuess,
                        };
                    },
                    token);
            }
            break;
        case GameScene.LiarGuess:
            {
                var round = game.Rounds.Last();
                if (game.Players.ExceptBy(round.LiarGuesses.Select(t => t.Player), p => p.Id).Any())
                {
                    return;
                }
                var liars = game.Topics.Where(p => round.Topic == p.Value).Select(p => p.Key).ToArray();
                foreach (var p in game.Players)
                {
                    p.CurrentScene = GameScene.RoundSummary;
                    var liar = round.LiarGuesses.Find(t => t.Player == p.Id)!;
                    if (liars.Contains(liar.Target))
                    {
                        p.Points += game.Config.CorrectPoint;
                    }
                }
                await cache.Set($"game/room/{game.Id}", game with { CurrentScene = GameScene.RoundSummary }, token);
            }
            break;
        case GameScene.RoundSummary:
            {
                if (game.Players.Any(p => p.CurrentScene != GameScene.WaitRoundStart))
                {
                    return;
                }
                if (game.Topics.Count == game.Rounds.Count)
                {
                    await cache.Set($"game/room/{game.Id}", game with
                    {
                        Players = [.. game.Players.Select(p => p with { CurrentScene = GameScene.GameEnd })],
                        CurrentScene = GameScene.GameEnd,
                    }, token);
                }
                else
                {
                    await StartNextRound(game, kernel, cache, token);
                }
            }
            break;
        case GameScene.GameEnd:
            break;
    }
}

// ルーム作成
api.MapPost("/createRoom", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromBody] CreateRoomRequest req) =>
{
    if (context.Session.Get<User>(nameof(User)) is not { } user)
    {
        user = new User(Guid.NewGuid(), req.Name);
        context.Session.Set(nameof(User), user);
    }

    if (string.IsNullOrEmpty(req.Aikotoba))
    {
        return Results.BadRequest("合言葉が必要です");
    }

    // テーマが未設定の場合はエラー
    if (string.IsNullOrEmpty(req.Theme))
    {
        return Results.BadRequest("ルーム作成時にはテーマの設定が必要です");
    }

    var room = await cache.GetStringAsync($"game/aikotoba/{req.Aikotoba}", context.RequestAborted);
    if (!string.IsNullOrEmpty(room))
    {
        return Results.BadRequest("この合言葉は既に使用されています");
    }

    var gameId = Guid.NewGuid().ToString();
    await cache.SetStringAsync($"game/aikotoba/{req.Aikotoba}", gameId, new() { SlidingExpiration = TimeSpan.FromHours(1) }, context.RequestAborted);

    var game = new Game(gameId, req.Aikotoba, req.Theme, [], [], [], GameScene.WaitRoundStart, new());
    var player = new Player(user.Id, user.Name);
    game.Players.Add(player);

    await cache.Set($"game/room/{gameId}", game, context.RequestAborted);
    await cache.SetStringAsync($"user/{user.Id}/room", gameId, new() { SlidingExpiration = TimeSpan.FromHours(1) }, context.RequestAborted);

    return Results.Ok(user);
});

// ルーム参加
api.MapPost("/joinRoom", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromBody] JoinRoomRequest req) =>
{
    if (context.Session.Get<User>(nameof(User)) is not { } user)
    {
        user = new User(Guid.NewGuid(), req.Name);
        context.Session.Set(nameof(User), user);
    }

    if (string.IsNullOrEmpty(req.Aikotoba))
    {
        return Results.BadRequest("合言葉が必要です");
    }

    var gameId = await cache.GetStringAsync($"game/aikotoba/{req.Aikotoba}", context.RequestAborted);
    if (string.IsNullOrEmpty(gameId))
    {
        return Results.BadRequest("指定された合言葉のルームが見つかりません");
    }

    var game = await cache.Get<Game>($"game/room/{gameId}", context.RequestAborted) ?? throw new InvalidOperationException("ゲームが見つかりません");

    if (game.Players.Any(p => p.Id == user.Id))
    {
        return Results.BadRequest("すでに登録済みです");
    }

    var player = new Player(user.Id, user.Name);
    game.Players.Add(player);

    await cache.Set($"game/room/{gameId}", game, context.RequestAborted);
    await cache.SetStringAsync($"user/{user.Id}/room", gameId, new() { SlidingExpiration = TimeSpan.FromHours(1) }, context.RequestAborted);

    return Results.Ok(user);
});

// お題登録
api.MapPost("/topic", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromBody] string topic) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("ユーザーが見つかりません");
    var game = await GetGameFromUser(user, cache, context.RequestAborted) ?? throw new InvalidOperationException("ゲームが見つかりません");

    if (string.IsNullOrEmpty(topic))
    {
        return Results.BadRequest("お題が必要です");
    }

    await cache.Update<Game>($"game/room/{game.Id}", g =>
    {
        g.Topics.Add(user.Id, topic);

        // お題登録後にプレイヤーの状態をWaitRoundStartに変更する
        var player = g.Players.First(p => p.Id == user.Id);
        player.CurrentScene = GameScene.WaitRoundStart;

        return g;
    }, context.RequestAborted);

    return Results.Ok();
});

var startLock = new KeyedAsyncLock();

// ラウンド開始
api.MapPost("/start", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromServices] Kernel kernel) =>
{
    var game = await GetCurrentGame(context, cache) ?? throw new InvalidOperationException("Game not found.");
    if (game.Players.Any(p => p.CurrentScene != GameScene.WaitRoundStart))
    {
        return Results.BadRequest("Some players are not ready.");
    }
    if (startLock.IsLocked(game.Id))
    {
        return Results.BadRequest("The round is already starting.");
    }
    using var l = await startLock.LockAsync(game.Id, context.RequestAborted);
    game = await GetCurrentGame(context, cache) ?? throw new InvalidOperationException("Game not found.");
    if (game.Rounds.Count > 0)
    {
        return Results.BadRequest("The round has already started.");
    }
    await StartNextRound(game, kernel, cache, context.RequestAborted);

    // ゲーム開始時にあいことばを消して使いまわせるようにする
    await cache.RemoveAsync($"game/aikotoba/{game.Aikotoba}", context.RequestAborted);
    return Results.Ok();
});

api.MapPost("/next", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromServices] Kernel kernel) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var game = await GetGameFromUser(user, cache, context.RequestAborted) ?? throw new InvalidOperationException("Game not found.");
    if (game.CurrentScene != GameScene.RoundSummary)
    {
        return Results.BadRequest("直前のラウンドが終わっていません");
    }
    game = await cache.Update<Game>($"game/room/{game.Id}", g =>
    {
        var player = g.Players.First(p => p.Id == user.Id);
        player.CurrentScene = GameScene.WaitRoundStart;
        return g;
    }, context.RequestAborted);
    await NextScene(cache, game, kernel, context.RequestAborted);
    return Results.Ok();
});

static async Task StartNextRound(Game game, Kernel kernel, IBufferDistributedCache cache, CancellationToken token = default)
{
    game = await cache.Update<Game>($"game/room/{game.Id}", g => g with { CurrentScene = GameScene.TopicSelecting }, token);
    var topics = game.Topics.Values.Except(game.Rounds.Select(r => r.Topic)).ToArray();
    var topic = topics[Random.Shared.Next(topics.Length)];
    var topicInfo = string.Join(Environment.NewLine, [
        await kernel.InvokeAsync<string>("search", "Search", new() { ["query"] = $"\"{topic}\" \"{game.Theme}\"" }, token),
        await kernel.InvokeAsync<string>("wiki", "Search", new() { ["query"] = $"intitle:\"{topic}\" deepcat:\"{game.Theme}\"" }, token),
        ]);
    var liars = game.Topics.Where(t => t.Value == topic).Select(t => t.Key).ToArray();
    var round = new Round(topic, topicInfo, liars, [], []);
    await cache.Update<Game>($"game/room/{game.Id}", g => g with
    {
        Players = [.. g.Players.Select(p => p with { CurrentScene = GameScene.QuestionAnswering })],
        CurrentScene = GameScene.QuestionAnswering,
        Rounds = [.. g.Rounds, round]
    }, token);
}

// 質問と回答
api.MapPost("/question", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromServices] Kernel kernel, [FromBody] string input) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var game = await GetGameFromUser(user, cache, context.RequestAborted) ?? throw new InvalidOperationException("Game not found.");
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
    var keywords = await kernel.GetRelationKeywords(round.Topic, input, game.Theme, geminiSettings);
    var prompt = new PromptTemplateConfig("""
        次の参考情報を基にして、ユーザーの質問に回答してください。

        ## テーマ
        {{$theme}}

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
        new() { ["topic"] = round.Topic, ["input"] = input, ["topicInfo"] = round.TopicInfo, ["keywords"] = keywords, ["theme"] = game.Theme });

    var res = result.GetFromJson<QuestionResponse>();
    await cache.Update<Game>(
        $"game/room/{game.Id}",
        g => g.Rounds.Last().Histories.Add(new(new QuestionResult(player.Id, input, res.Result), res.Reason, result.RenderedPrompt ?? string.Empty)),
        context.RequestAborted);
    return res.Result;
});

api.MapGet("/{room}/round/{i}/history", async ([FromServices] IBufferDistributedCache cache, [FromRoute] string room, [FromRoute] int i)
    => (await cache.Get<Game>($"game/room/{room}"))?.Rounds[i].Histories.Select(h => h.Result));
api.MapGet("/{room}/round/{i}/history/internal", async ([FromServices] IBufferDistributedCache cache, [FromRoute] string room, int i)
    => (await cache.Get<Game>($"game/room/{room}"))?.Rounds[i].Histories);

// ユーザーの解答と結果
api.MapPost("/answer", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromServices] Kernel kernel, [FromBody] string input) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var game = await GetGameFromUser(user, cache, context.RequestAborted) ?? throw new InvalidOperationException("Game not found.");
    var round = game.Rounds.Last();
    if (round.Histories.Select(h => h.Result).OfType<AnswerResult>().Count(h => h.Player == user.Id) > game.Config.AnswerLimit)
    {
        throw new InvalidOperationException("You have reached the answer limit.");
    }
    if (input == round.Topic)
    {
        game = await cache.Update<Game>(
            $"game/room/{game.Id}",
            g =>
            {
                var player = g.Players.First(p => p.Id == user.Id);
                player.Points += g.Config.CorrectPoint;
                g.Rounds.Last().Histories.Add(new(new AnswerResult(player.Id, input, AnswerResultType.Correct), "完全一致", string.Empty));
                return g with
                {
                    Players = [.. g.Players.Select(p => p with { CurrentScene = GameScene.LiarGuess })],
                    CurrentScene = GameScene.LiarGuess,
                };
            },
            context.RequestAborted);
        return AnswerResultType.Correct;
    }
    var keywords = await kernel.GetRelationKeywords(round.Topic, input, game.Theme, geminiSettings);
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
    game = await cache.Update<Game>(
        $"game/room/{game.Id}",
        g => g.Rounds.Last().Histories.Add(new(new AnswerResult(user.Id, input, res.Result), res.Reason, result.RenderedPrompt ?? string.Empty)),
        context.RequestAborted);
    if (res.Result == AnswerResultType.Correct)
    {
        game = await cache.Update<Game>(
            $"game/room/{game.Id}",
            g =>
            {
                var player = g.Players.First(p => p.Id == user.Id);
                player.Points += g.Config.CorrectPoint;
                return g with
                {
                    Players = [.. g.Players.Select(p => p with { CurrentScene = GameScene.LiarGuess })],
                    CurrentScene = GameScene.LiarGuess,
                };
            },
            context.RequestAborted);
        return AnswerResultType.Correct;
    }
    await NextScene(cache, game, kernel, context.RequestAborted);
    return res.Result;
});

// 嘘をついているプレイヤーの推理
api.MapPost("/guess", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromServices] Kernel kernel, [FromBody] Guid target) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var game = await GetGameFromUser(user, cache, context.RequestAborted) ?? throw new InvalidOperationException("Game not found.");
    var round = game.Rounds.Last();
    round.LiarGuesses.Add(new(user.Id, target));
    await cache.Set($"game/room/{game.Id}", game, context.RequestAborted);
    await NextScene(cache, game, kernel, context.RequestAborted);
});

// プレイヤーのバン
api.MapPost("/ban", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromServices] Kernel kernel, [FromBody] Guid target) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var game = await GetGameFromUser(user, cache, context.RequestAborted) ?? throw new InvalidOperationException("Game not found.");
    var player = game.Players.First(p => p.Id == user.Id);
    if (game.Players.IndexOf(player) == 0)
    {
        game.Players.RemoveAll(p => p.Id == target);
    }
    else if (player.Id == target)
    {
        game.Players.Remove(player);
    }
    else
    {
        return Results.BadRequest("You can't ban this player.");
    }
    // バンされたプレイヤーのお題が使われていない場合、お題を削除
    if (game.Rounds.All(r => !r.Liars.Contains(target)))
    {
        game.Topics.Remove(target);
    }
    await cache.RemoveAsync($"user/{target}/room");
    if (game.Players.Count == 0)
    {
        await cache.RemoveAsync($"game/aikotoba/{game.Aikotoba}");
        await cache.RemoveAsync($"game/room/{game.Id}");
        return Results.Ok();
    }
    else
    {
        await cache.Set($"game/room/{game.Id}", game, context.RequestAborted);
        await NextScene(cache, game, kernel, context.RequestAborted);
        return Results.Ok();
    }
});

// シーン情報取得
api.MapGet("/scene", async (HttpContext context, [FromServices] IBufferDistributedCache cache)
    => await GetCurrentGame(context, cache) is not { } game
        ? Results.NotFound()
        : Results.Ok(new CurrentScene(
            game.Id,
            game.Aikotoba,
            game.Theme,
            game.CurrentScene,
            game.Rounds.Count,
            game.Players.ToArray(),
            game.CurrentScene switch
            {
                GameScene.RegisterTopic
                    => new EmptySceneInfo(),
                GameScene.WaitRoundStart
                    => new WaitRoundSceneInfo(
                        game.Players.Where(p => p.CurrentScene == game.CurrentScene).Count()),
                GameScene.TopicSelecting
                    => new TopicSelectingSceneInfo(),
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
            })));

// プレイヤーのシーン情報更新
api.MapPost("/scene", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromBody] GameScene scene) =>
{
    var user = context.Session.Get<User>(nameof(User)) ?? throw new InvalidOperationException("User not found.");
    var game = await GetGameFromUser(user, cache, context.RequestAborted) ?? throw new InvalidOperationException("Game not found.");
    var player = game.Players.First(p => p.Id == user.Id);
    player.CurrentScene = scene;
    await cache.Set($"game/room/{game.Id}", game, context.RequestAborted);
});

// プレイヤー情報取得
api.MapGet("/me", (HttpContext context) => context.Session.Get<User>(nameof(User)) is { } u ? Results.Ok(u) : Results.NotFound());

// ゲームリセット
api.MapPost("/reset", async (HttpContext context, [FromServices] IBufferDistributedCache cache) =>
{
    var game = await GetCurrentGame(context, cache);
    if (game is null)
    {
        return Results.NotFound();
    }
    foreach (var player in game.Players)
    {
        await cache.RemoveAsync($"user/{player.Id}/room", context.RequestAborted);
    }
    await cache.RemoveAsync($"game/room/{game.Id}", context.RequestAborted);
    var room = await cache.GetStringAsync($"game/aikotoba/{game.Aikotoba}", context.RequestAborted);
    if (room == game.Id)
    {
        await cache.RemoveAsync($"game/aikotoba/{game.Aikotoba}", context.RequestAborted);
    }
    return Results.Ok();
});

// 設定取得
api.MapGet("/config", async (HttpContext context, [FromServices] IBufferDistributedCache cache)
    => (await GetCurrentGame(context, cache))?.Config ?? new());

// 設定更新
api.MapPost("/config", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromBody] Config newConfig) =>
{
    if (await GetCurrentGame(context, cache) is not { } game)
    {
        return Results.BadRequest("Game not found.");
    }
    await cache.Update<Game>($"game/room/{game.Id}", g => g with { Config = newConfig }, context.RequestAborted);
    return Results.Ok();
});

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

// ルーム作成リクエスト
record CreateRoomRequest(string Name, string Aikotoba, string Theme);

// ルーム参加リクエスト
record JoinRoomRequest(string Name, string Aikotoba);

record SemanticKernelOptions(string ModelId, string ApiKey, string BingKey, GoogleSearchParam GoogleSearch);
record QuestionResponse(string Reason, QuestionResultType Result);
record AnswerResponse(string Reason, AnswerResultType Result);

record CurrentScene(string Id, string Aikotoba, string Theme, GameScene Scene, int Round, Player[] Players, ISceneInfo Info);

[JsonDerivedType(typeof(EmptySceneInfo))]
[JsonDerivedType(typeof(WaitRoundSceneInfo))]
[JsonDerivedType(typeof(TopicSelectingSceneInfo))]
[JsonDerivedType(typeof(QuestionAnsweringSceneInfo))]
[JsonDerivedType(typeof(LiarGuessSceneInfo))]
[JsonDerivedType(typeof(RoundSummaryInfo))]
[JsonDerivedType(typeof(GameEndInfo))]
interface ISceneInfo;

record WaitRoundSceneInfo(int Waiting) : ISceneInfo;
record TopicSelectingSceneInfo() : ISceneInfo;
record QuestionAnsweringSceneInfo(IPlayerResult[] Histories) : ISceneInfo;
record LiarGuessSceneInfo(string Topic, Guid[] TopicCorrectPlayers, LiarGuess[] Targets) : ISceneInfo;
record RoundSummaryInfo(string Topic, Guid[] TopicCorrectPlayers, Guid[] LiarCorrectPlayers) : ISceneInfo;
record RoundResult(string Topic, Guid[] TopicCorrectPlayers, Guid[] LiarPlayers, Guid[] LiarCorrectPlayers);
record GameEndInfo(RoundResult[] Results) : ISceneInfo;
record EmptySceneInfo() : ISceneInfo;

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

    public static async Task<string> GetRelationKeywords(this Kernel kernel, string correct, string target, string theme, PromptExecutionSettings settings)
    {
        var ptompt = new PromptTemplateConfig("""
        対象の2つの単語、文章の関係性を検索エンジンで調査するためのキーワードを生成してください。

        ## テーマ
        {{$theme}}

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
            InputVariables = [new() { Name = "correct" }, new() { Name = "target" }, new() { Name = "theme" }],
        };
        ptompt.AddExecutionSettings(settings);
        var keywordsFunc = kernel.CreateFunctionFromPrompt(ptompt);
        return await keywordsFunc.InvokeAsync<string>(kernel, new() { ["correct"] = correct, ["target"] = target, ["theme"] = theme }) ?? string.Empty;
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
