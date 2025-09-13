using System.Reflection;
using System.Text.Json.Serialization;
using CommunityToolkit.HighPerformance;
using GenerativeAI;
using GenerativeAI.Types;
using GenerativeAI.Web;
using GoogleTrendsApi;
using Korenan.ApiService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web.Tavily;
using GoogleTrends = GoogleTrendsApi.Api;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();
builder.Services.AddHostedService<BotService>();

// Add services to the container.
builder.Services.AddProblemDetails();

var (modelId, apiKey, tavilyApiKey) = builder.Configuration.GetSection(nameof(SemanticKernelOptions)).Get<SemanticKernelOptions>()!;
var kernelBuikder = builder.Services.AddKernel()
    .AddGoogleAIGeminiChatCompletion(modelId, apiKey)
    .AddGoogleAIEmbeddingGenerator(modelId, apiKey);
kernelBuikder.Plugins.AddFromFunctions();
builder.Services.AddGenerativeAI(new GenerativeAIOptions { Credentials = new(apiKey), Model = modelId });

builder.AddRedisDistributedCache("cache");
builder.Services.AddHttpClient(string.Empty, b =>
    {
        b.DefaultRequestHeaders.UserAgent.ParseAdd("korenan/1.0");
    });

builder.Services
    .ConfigureHttpJsonOptions(op => op.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddTavilyTextSearch(tavilyApiKey, new() { IncludeAnswer = true, SearchDepth = TavilySearchDepth.Advanced })
    .AddSingleton(sp => sp.GetRequiredService<ITextSearch>().CreateWithSearch("search"))
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
                // すべてのプレイヤーが解答制限に達した場合、ライアー推理シーンに遷移
                var playerAnswers = round.Histories.Select(h => h.Result).OfType<AnswerResult>().GroupBy(h => h.Player).ToDictionary(g => g.Key, g => g.Count());
                if (game.Players.Select(p => playerAnswers.TryGetValue(p.Id, out var count) ? count : 0).Any(c => c < game.Config.AnswerLimit))
                {
                    break;
                }
                await cache.Update<Game>(
                    $"game/room/{game.Id}",
                    g => g with
                    {
                        Players = [.. g.Players.Select(p => p with { CurrentScene = GameScene.LiarGuess })],
                        CurrentScene = GameScene.LiarGuess,
                    },
                    token);
            }
            break;
        case GameScene.LiarGuess:
            {
                // 最新ラウンドで全員がライアー指摘を終えていなければ何もしない
                if (game.Players.ExceptBy(game.Rounds[^1].LiarGuesses.Select(t => t.Player), p => p.Id).Any())
                {
                    return;
                }

                // 全ラウンド終了していなければ次のラウンドを開始
                if (game.Topics.Count != game.Rounds.Count)
                {
                    await StartNextRound(game, kernel, null, cache, token);
                }
                else
                {
                    // ライアーポイントの計算
                    foreach (var round in game.Rounds)
                    {
                        var liars = game.Topics.Where(p => round.Topic == p.Value).Select(p => p.Key).ToArray();
                        // 誰もお題を当てられなかった場合、ライアーは正解者なしとしてポイントを獲得
                        if (!round.Histories.Any(h => h.Result is AnswerResult { Result: AnswerResultType.Correct }))
                        {
                            foreach (var liar in liars)
                            {
                                var player = game.Players.First(p => p.Id == liar);
                                player.Points += game.Config.NoCorrectPoint;
                            }
                        }
                        // ライアーを当てた場合、正解者はポイントを獲得
                        foreach (var p in game.Players)
                        {
                            var liar = round.LiarGuesses.Find(t => t.Player == p.Id)!;
                            if (liars.Contains(liar.Target))
                            {
                                p.Points += game.Config.LiarPoint;
                            }
                        }
                    }

                    await cache.Set($"game/room/{game.Id}", game with
                    {
                        Players = [.. game.Players.Select(p => p with { CurrentScene = GameScene.GameEnd })],
                        CurrentScene = GameScene.GameEnd,
                    }, token);
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
api.MapPost("/start", async (HttpContext context, [FromServices] IBufferDistributedCache cache, [FromServices] Kernel kernel, [FromServices] IGenerativeAiService aiService) =>
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
    await StartNextRound(game, kernel, aiService, cache, context.RequestAborted);

    // ゲーム開始時にあいことばを消して使いまわせるようにする
    await cache.RemoveAsync($"game/aikotoba/{game.Aikotoba}", context.RequestAborted);
    return Results.Ok();
});

static async Task StartNextRound(Game game, Kernel kernel, IGenerativeAiService? aiService, IBufferDistributedCache cache, CancellationToken token = default)
{
    game = await cache.Update<Game>($"game/room/{game.Id}", g => g with { CurrentScene = GameScene.TopicSelecting }, token);
    async Task<Round> GenRound()
    {
        var topics = game.Topics.Values.Except(game.Rounds.Select(r => r.Topic)).ToArray();
        var topic = topics[Random.Shared.Next(topics.Length)];
        var topicInfo = string.Join(Environment.NewLine, [
            .. await kernel.InvokeAsync<List<string>>("search", "Search", new() { ["query"] = $"{game.Theme}の{topic}の概要" }, token) ?? [],
                await kernel.InvokeAsync<string>("wiki", "Search", new() { ["query"] = $"intitle:\"{topic}\" deepcat:\"{game.Theme}\"" }, token),
            ]);
        topicInfo = await kernel.Summary(game.Theme, topic, topicInfo);
        var liars = game.Topics.Where(t => t.Value == topic).Select(t => t.Key).ToArray();
        return new Round(topic, topicInfo, liars, [], []);
    }
    async Task<Round?> GenImage()
    {
        if (aiService is null)
        {
            return null;
        }
        var theme = game.Theme;
        var img = await GenerateImage(aiService, $"""
            The attached image is an AI character.
            You must output an image where this character has been rewritten to possess characteristics that evoke an image of someone knowledgeable about "{theme}".
            Generate an illustration that incorporates elements related to "{theme}" while utilizing the image's existing features.
            For example, have the character hold items or symbols related to "{theme}", or depict scenery in the background that evokes associations with "{theme}".
            Do not include text in the image.
            """,
            GetEmbeddedImageResource());
        await cache.SetAsync($"game/image/bot/{game.Id}", img, new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(1) }, token);
        return null;
    }
    var results = await Task.WhenAll(GenRound(), GenImage()!);
    await cache.Update<Game>($"game/room/{game.Id}", g => g with
    {
        Players = [.. g.Players.Select(p => p with { CurrentScene = GameScene.QuestionAnswering })],
        CurrentScene = GameScene.QuestionAnswering,
        Rounds = [.. g.Rounds, results[0]]
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
    var keywords = await kernel.GetRelationKeywords(round.Topic, input, game.Theme);
    var res = await kernel.GetAwnser(game.Theme, round, input, keywords);
    await cache.Update<Game>(
        $"game/room/{game.Id}",
        g => g.Rounds.Last().Histories.Add(new(new QuestionResult(player.Id, input, res.Result), res.Reason, DateTime.UtcNow)),
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
                g.Rounds.Last().Histories.Add(new(new AnswerResult(player.Id, input, AnswerResultType.Correct), "完全一致", DateTime.UtcNow));
                return g with
                {
                    Players = [.. g.Players.Select(p => p with { CurrentScene = GameScene.LiarGuess })],
                    CurrentScene = GameScene.LiarGuess,
                };
            },
            context.RequestAborted);
        return AnswerResultType.Correct;
    }
    var keywords = await kernel.GetRelationKeywords(round.Topic, input, game.Theme);
    var res = await kernel.IsAnswer(round, input, keywords);

    game = await cache.Update<Game>(
        $"game/room/{game.Id}",
        g => g.Rounds.Last().Histories.Add(new(new AnswerResult(user.Id, input, res.Result), res.Reason, DateTime.UtcNow)),
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

    // 自分自身を指摘することはできない
    if (user.Id == target)
    {
        return Results.BadRequest("自分自身をライアーとして指摘することはできません。");
    }

    var round = game.Rounds.Last();
    round.LiarGuesses.Add(new(user.Id, target));
    await cache.Set($"game/room/{game.Id}", game, context.RequestAborted);
    await NextScene(cache, game, kernel, context.RequestAborted);

    return Results.Ok();
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
                        [.. game.Rounds.Last().LiarGuesses.Select(g => g.Player)],
                        [.. game.Rounds.Last().Histories]),
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

api.MapGet("/image/bot", async (HttpContext context, [FromServices] IBufferDistributedCache cache) =>
{
    if (await GetCurrentGame(context, cache) is not { } game)
    {
        return Results.NotFound();
    }
    var img = await cache.GetAsync($"game/image/bot/{game.Id}", context.RequestAborted);
    return img is null ? Results.NotFound() : Results.File(img, "image/png");
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
api.MapGet("/gen-image", async ([FromServices] IGenerativeAiService aiService, [FromQuery] string theme) =>
{
    var img = await GenerateImage(aiService, $"""
        The attached image is an AI character.
        You must output an image where this character has been rewritten to possess characteristics that evoke an image of someone knowledgeable about "{theme}".
        Generate an illustration that incorporates elements related to "{theme}" while utilizing the image's existing features.
        For example, have the character hold items or symbols related to "{theme}", or depict scenery in the background that evokes associations with "{theme}".
        Do not include text in the image.
        """,
        GetEmbeddedImageResource());
    if (img.Length == 0)
    {
        return Results.NotFound();
    }
    return Results.File(img, "image/png");
});
#endif

static byte[] GetEmbeddedImageResource()
{
    var assembly = Assembly.GetExecutingAssembly();
    var resourceName = "Korenan.ApiService.ai_character01_smile.png";
    using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException($"埋め込みリソース '{resourceName}' が見つかりません。");
    using var memoryStream = new MemoryStream();
    stream.CopyTo(memoryStream);
    return memoryStream.ToArray();
}

static async Task<byte[]> GenerateImage(IGenerativeAiService aiService, string prompt, byte[] baseImage)
{
    var model = aiService.CreateInstance("gemini-2.5-flash-image-preview");
    var content = new Content(prompt, Roles.User);
    content.AddInlineData(Convert.ToBase64String(baseImage), "image/png");
    var res = await model.GenerateContentAsync(new GenerateContentRequest(content));
    return res.Candidates?.FirstOrDefault()?.Content?.Parts.Select(p => p.InlineData).OfType<Blob>().FirstOrDefault() is { Data: { } data }
        ? Convert.FromBase64String(data)
        : [];
}

app.MapDefaultEndpoints();
app.MapFallbackToFile("/index.html");
app.Run();

// ルーム作成リクエスト
record CreateRoomRequest(string Name, string Aikotoba, string Theme);

// ルーム参加リクエスト
record JoinRoomRequest(string Name, string Aikotoba);

record SemanticKernelOptions(string ModelId, string ApiKey, string TavilyApiKey);
record QuestionResponse(string Reason, QuestionResultType Result);
record AnswerResponse(string Reason, AnswerResultType Result);

record CurrentScene(string Id, string Aikotoba, string Theme, GameScene Scene, int Round, Player[] Players, ISceneInfo Info);

[JsonDerivedType(typeof(EmptySceneInfo))]
[JsonDerivedType(typeof(WaitRoundSceneInfo))]
[JsonDerivedType(typeof(TopicSelectingSceneInfo))]
[JsonDerivedType(typeof(QuestionAnsweringSceneInfo))]
[JsonDerivedType(typeof(LiarGuessSceneInfo))]
[JsonDerivedType(typeof(GameEndInfo))]
interface ISceneInfo;

record WaitRoundSceneInfo(int Waiting) : ISceneInfo;
record TopicSelectingSceneInfo() : ISceneInfo;
record QuestionAnsweringSceneInfo(IPlayerResult[] Histories) : ISceneInfo;
record LiarGuessSceneInfo(string Topic, Guid[] TopicCorrectPlayers, Guid[] GuessedPlayers, HistoryInfo[] Histories) : ISceneInfo;
record RoundResult(string Topic, Guid[] TopicCorrectPlayers, Guid[] LiarPlayers, Guid[] LiarCorrectPlayers);
record GameEndInfo(RoundResult[] Results) : ISceneInfo;
record EmptySceneInfo() : ISceneInfo;
