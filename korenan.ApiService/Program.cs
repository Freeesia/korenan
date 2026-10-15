using System.ClientModel;
using System.Collections.Immutable;
using System.Net.ServerSentEvents;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Korenan.ApiService;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using OpenAI;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();
builder.AddRedisDistributedCache("cache");

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(AiTelemetry.SourceName))
    .WithMetrics(metrics => metrics.AddMeter(AiTelemetry.SourceName));

var aiOptions = builder.Configuration.GetSection("AI").Get<AiOptions>() ?? new();
var models = aiOptions.ResolveModels();
var tavilyApiKey = builder.Configuration["Tavily:ApiKey"] ?? string.Empty;
var enableSensitiveData = builder.Environment.IsDevelopment();

if (aiOptions.Provider == AiProvider.OpenAI)
{
    builder.Services.AddSingleton(_ => new OpenAIClient(
        new ApiKeyCredential(aiOptions.ApiKey),
        new OpenAIClientOptions { Endpoint = aiOptions.Endpoint }));
}
else
{
    builder.Services.AddSingleton(_ => new Google.GenAI.Client(apiKey: aiOptions.ApiKey));
}

IChatClient CreateChatClient(IServiceProvider sp, string model) => aiOptions.Provider switch
{
    AiProvider.OpenAI => sp.GetRequiredService<OpenAIClient>().GetChatClient(model).AsIChatClient(),
    _ => sp.GetRequiredService<Google.GenAI.Client>().AsIChatClient(model),
};

foreach (var (key, model) in new[] { (AiModel.Judge, models.Judge), (AiModel.Light, models.Light), (AiModel.Bot, models.Bot) })
{
    builder.Services.AddKeyedChatClient(key, sp => CreateChatClient(sp, model))
        .UseLogging()
        .UseOpenTelemetry(sourceName: AiTelemetry.SourceName, configure: c => c.EnableSensitiveData = enableSensitiveData);
}

if (!string.IsNullOrEmpty(models.Image))
{
    builder.Services.AddImageGenerator(sp => aiOptions.Provider switch
        {
            AiProvider.OpenAI => sp.GetRequiredService<OpenAIClient>().GetImageClient(models.Image).AsIImageGenerator(),
            _ => new ChatImageGenerator(CreateChatClient(sp, models.Image)),
        })
        .UseLogging()
        .UseOpenTelemetry(sourceName: AiTelemetry.SourceName, configure: c => c.EnableSensitiveData = enableSensitiveData);
}

builder.Services.AddHttpClient(nameof(TavilySearch), c =>
{
    c.BaseAddress = new("https://api.tavily.com/");
    c.DefaultRequestHeaders.Authorization = new("Bearer", tavilyApiKey);
});
builder.Services.AddHttpClient(nameof(WikipediaSearch), c => c.DefaultRequestHeaders.UserAgent.ParseAdd("korenan/1.0"));

builder.Services
    .AddProblemDetails()
    .AddOpenApi()
    .ConfigureHttpJsonOptions(op => op.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddSingleton(sp => sp.GetRequiredService<IDistributedCache>() as IBufferDistributedCache ?? throw new InvalidOperationException("内部的にはIBufferDistributedCacheで実装されているはず"))
    .AddSingleton<RoomNotifier>()
    .AddSingleton<GameStore>()
    .AddSingleton<PromptTemplates>()
    .AddSingleton<TavilySearch>()
    .AddSingleton<WikipediaSearch>()
    .AddSingleton<GameAi>()
    .AddSession(op =>
    {
        op.IdleTimeout = TimeSpan.FromDays(30);
        op.Cookie.HttpOnly = true;
        op.Cookie.IsEssential = true;
    });

// ビルド時のOpenAPIドキュメント生成ではボットを起動しない
if (Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
{
    builder.Services.AddHostedService<BotService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapOpenApi();
app.MapScalarApiReference();

app.UseSession();

var api = app.MapGroup("/api");

static ProblemHttpResult BadRequestProblem(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
static ProblemHttpResult NotFoundProblem(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status404NotFound);
static ProblemHttpResult ForbiddenProblem(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status403Forbidden);

static User? GetUser(HttpContext context) => context.Session.Get<User>(nameof(User));

static async Task<PlayerContext?> GetPlayerContext(HttpContext context, GameStore store)
{
    if (GetUser(context) is not { } user)
    {
        return null;
    }
    return await store.GetByUser(user.Id, context.RequestAborted) is { } game ? new(user, game) : null;
}

static User SignIn(HttpContext context, string name)
{
    var user = (GetUser(context) ?? new User(Guid.NewGuid(), name)) with { Name = name };
    context.Session.Set(nameof(User), user);
    return user;
}

// 状態に応じてシーンを進める
static Game Advance(Game game) => game switch
{
    { Players.IsEmpty: true } => game,
    { CurrentScene: GameScene.QuestionAnswering, Rounds: [.., var round] }
        when round.GetCorrectPlayers().Any() || game.Players.All(p => round.CountAnswers(p.Id) >= game.Config.AnswerLimit)
        => game.WithScene(GameScene.LiarGuess),
    { CurrentScene: GameScene.LiarGuess, Rounds: [.., var round] }
        when game.Players.All(p => round.LiarGuesses.Any(g => g.Player == p.Id))
        => game.GetRemainingTopics().Length > 0 ? game.WithScene(GameScene.TopicSelecting) : Finish(game),
    _ => game,
};

// 最終的なポイントを計算してゲームを終了する
static Game Finish(Game game)
{
    var points = game.Players.ToDictionary(p => p.Id, p => p.Points);
    foreach (var round in game.Rounds)
    {
        // 誰もお題を当てられなかった場合、ライアーは正解者なしとしてポイントを獲得
        if (!round.GetCorrectPlayers().Any())
        {
            foreach (var liar in round.Liars.Where(points.ContainsKey))
            {
                points[liar] += game.Config.NoCorrectPoint;
            }
        }
        // ライアーを当てた場合、正解者はポイントを獲得
        foreach (var guess in round.LiarGuesses.Where(g => round.Liars.Contains(g.Target) && points.ContainsKey(g.Player)))
        {
            points[guess.Player] += game.Config.LiarPoint;
        }
    }
    return (game with { Players = [.. game.Players.Select(p => p with { Points = points[p.Id] })] }).WithScene(GameScene.GameEnd);
}

// ゲームを更新してシーンを進め、新しいラウンドが必要であれば準備する
static async Task<Game> UpdateAndAdvance(GameStore store, GameAi ai, string roomId, Func<Game, Game> update, CancellationToken token)
{
    var nextRound = false;
    var game = await store.Update(roomId, g =>
    {
        var updated = Advance(update(g));
        nextRound = g.CurrentScene != GameScene.TopicSelecting && updated.CurrentScene == GameScene.TopicSelecting;
        return updated;
    }, token);
    if (nextRound)
    {
        game = await PrepareRound(store, ai, game, token);
    }
    return game;
}

// お題を抽選してラウンドを開始する
static async Task<Game> PrepareRound(GameStore store, GameAi ai, Game game, CancellationToken token)
{
    var topics = game.GetRemainingTopics();
    var topic = topics[Random.Shared.Next(topics.Length)];
    var topicInfo = await ai.CollectTopicInfo(game.Theme, topic, token);
    var liars = game.Topics.Where(t => t.Value == topic).Select(t => t.Key).ToImmutableArray();
    return await store.Update(
        game.Id,
        g => g.WithScene(GameScene.QuestionAnswering) with { Rounds = g.Rounds.Add(new(topic, topicInfo, liars, [], [])) },
        token);
}

static CurrentScene ToCurrentScene(Game game) => new(
    game.Id,
    game.Aikotoba,
    game.Theme,
    game.CurrentScene,
    game.Rounds.Length,
    game.HostId,
    [.. game.Players],
    game switch
    {
        { CurrentScene: GameScene.WaitRoundStart }
            => new WaitRoundSceneInfo(game.Players.Count(p => p.CurrentScene == game.CurrentScene)),
        { CurrentScene: GameScene.TopicSelecting }
            => new TopicSelectingSceneInfo(),
        { CurrentScene: GameScene.QuestionAnswering, Rounds: [.., var round] }
            => new QuestionAnsweringSceneInfo([.. round.Histories.Select(h => h.Result)]),
        { CurrentScene: GameScene.LiarGuess, Rounds: [.., var round] }
            => new LiarGuessSceneInfo(
                round.Topic,
                [.. round.GetCorrectPlayers()],
                [.. round.LiarGuesses.Select(g => g.Player)],
                [.. round.Histories]),
        { CurrentScene: GameScene.GameEnd }
            => new GameEndInfo([.. game.Rounds.Select(r => new RoundResult(
                r.Topic,
                [.. r.GetCorrectPlayers()],
                [.. r.Liars],
                [.. r.LiarGuesses.Where(t => r.Liars.Contains(t.Target)).Select(t => t.Player)]))]),
        _ => new EmptySceneInfo(),
    });

static async IAsyncEnumerable<SseItem<CurrentScene?>> StreamScene(User? user, GameStore store, RoomNotifier notifier, [EnumeratorCancellation] CancellationToken token)
{
    if (user is null || await store.GetUserRoom(user.Id, token) is not { } roomId)
    {
        yield return new(null, "none");
        yield break;
    }

    using var subscription = notifier.Subscribe(roomId);
    var game = await store.Get(roomId, token);
    var pending = subscription.Reader.WaitToReadAsync(token).AsTask();
    while (!token.IsCancellationRequested)
    {
        if (game is null || game.Players.All(p => p.Id != user.Id))
        {
            yield return new(null, "none");
            yield break;
        }
        yield return new(ToCurrentScene(game), "scene");

        while (!token.IsCancellationRequested && await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(15), token)) != pending)
        {
            yield return new(null, "ping");
        }
        if (!await pending || !subscription.Reader.TryRead(out game))
        {
            yield break;
        }
        pending = subscription.Reader.WaitToReadAsync(token).AsTask();
    }
}

// ルーム作成
api.MapPost("/createRoom", async Task<Results<Ok<User>, ProblemHttpResult>> (HttpContext context, GameStore store, CreateRoomRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name))
    {
        return BadRequestProblem("名前が必要です");
    }
    if (string.IsNullOrWhiteSpace(req.Aikotoba))
    {
        return BadRequestProblem("合言葉が必要です");
    }
    if (string.IsNullOrWhiteSpace(req.Theme))
    {
        return BadRequestProblem("ルーム作成時にはテーマの設定が必要です");
    }
    if (await store.FindRoomByAikotoba(req.Aikotoba, context.RequestAborted) is not null)
    {
        return BadRequestProblem("この合言葉は既に使用されています");
    }

    var user = SignIn(context, req.Name);
    var gameId = Guid.NewGuid().ToString();
    await store.ReserveAikotoba(req.Aikotoba, gameId, context.RequestAborted);
    var game = new Game(
        gameId,
        req.Aikotoba,
        req.Theme,
        [new Player(user.Id, user.Name)],
        ImmutableDictionary<Guid, string>.Empty,
        [],
        GameScene.WaitRoundStart,
        new(),
        user.Id);
    await store.Create(game, context.RequestAborted);
    await store.SetUserRoom(user.Id, gameId, context.RequestAborted);
    return TypedResults.Ok(user);
});

// ルーム参加
api.MapPost("/joinRoom", async Task<Results<Ok<User>, ProblemHttpResult>> (HttpContext context, GameStore store, JoinRoomRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name))
    {
        return BadRequestProblem("名前が必要です");
    }
    if (string.IsNullOrWhiteSpace(req.Aikotoba))
    {
        return BadRequestProblem("合言葉が必要です");
    }
    if (await store.FindRoomByAikotoba(req.Aikotoba, context.RequestAborted) is not { } gameId
        || await store.Get(gameId, context.RequestAborted) is null)
    {
        return BadRequestProblem("指定された合言葉のルームが見つかりません");
    }

    var user = SignIn(context, req.Name);
    await store.Update(
        gameId,
        g => g.Players.Any(p => p.Id == user.Id)
            ? g.UpdatePlayer(user.Id, p => p with { Name = user.Name })
            : g with { Players = g.Players.Add(new Player(user.Id, user.Name)) },
        context.RequestAborted);
    await store.SetUserRoom(user.Id, gameId, context.RequestAborted);
    return TypedResults.Ok(user);
});

// お題登録
api.MapPost("/topic", async Task<Results<Ok, ProblemHttpResult>> (HttpContext context, GameStore store, [FromBody] string topic) =>
{
    if (await GetPlayerContext(context, store) is not { User: var user, Game: var game })
    {
        return NotFoundProblem("ゲームが見つかりません");
    }
    if (string.IsNullOrWhiteSpace(topic))
    {
        return BadRequestProblem("お題が必要です");
    }
    if (!game.Rounds.IsEmpty)
    {
        return BadRequestProblem("ゲーム開始後はお題を変更できません");
    }

    await store.Update(
        game.Id,
        g => (g with { Topics = g.Topics.SetItem(user.Id, topic) }).UpdatePlayer(user.Id, p => p with { CurrentScene = GameScene.WaitRoundStart }),
        context.RequestAborted);
    return TypedResults.Ok();
});

// ラウンド開始
api.MapPost("/start", async Task<Results<Ok, ProblemHttpResult>> (HttpContext context, GameStore store, GameAi ai) =>
{
    if (await GetPlayerContext(context, store) is not { Game: var game })
    {
        return NotFoundProblem("ゲームが見つかりません");
    }

    var started = false;
    game = await store.Update(game.Id, g =>
    {
        started = g is { CurrentScene: GameScene.WaitRoundStart, Rounds.IsEmpty: true }
            && g.Players.All(p => p.CurrentScene == GameScene.WaitRoundStart && g.Topics.ContainsKey(p.Id));
        return started ? g.WithScene(GameScene.TopicSelecting) : g;
    }, context.RequestAborted);
    if (!started)
    {
        return BadRequestProblem("ゲームを開始できる状態ではありません");
    }

    // ゲーム開始時にあいことばを消して使いまわせるようにする
    if (await store.FindRoomByAikotoba(game.Aikotoba) == game.Id)
    {
        await store.ReleaseAikotoba(game.Aikotoba);
    }

    async Task GenerateBotImage() => await store.SetBotImage(game.Id, await ai.GenerateCharacterImage(game.Theme));
    await Task.WhenAll(PrepareRound(store, ai, game, CancellationToken.None), GenerateBotImage());
    return TypedResults.Ok();
});

// 質問と回答
api.MapPost("/question", async Task<Results<Ok<QuestionResultType>, ProblemHttpResult>> (HttpContext context, GameStore store, GameAi ai, [FromBody] string input) =>
{
    if (await GetPlayerContext(context, store) is not { User: var user, Game: var game })
    {
        return NotFoundProblem("ゲームが見つかりません");
    }
    if (game is not { CurrentScene: GameScene.QuestionAnswering, Rounds: [.., var round] })
    {
        return BadRequestProblem("質問できるシーンではありません");
    }
    if (string.IsNullOrWhiteSpace(input))
    {
        return BadRequestProblem("質問が必要です");
    }
    if (round.CountQuestions(user.Id) >= game.Config.QuestionLimit)
    {
        return BadRequestProblem("質問回数の上限に達しました");
    }

    var res = await ai.Answer(game.Theme, round, input, context.RequestAborted);
    var roundCount = game.Rounds.Length;
    await store.Update(
        game.Id,
        g => g is { CurrentScene: GameScene.QuestionAnswering } && g.Rounds.Length == roundCount && g.Rounds[^1].CountQuestions(user.Id) < g.Config.QuestionLimit
            ? g.AddHistory(new(new QuestionResult(user.Id, input, res.Result), res.Reason, DateTime.UtcNow))
            : g,
        context.RequestAborted);
    return TypedResults.Ok(res.Result);
});

api.MapGet("/{room}/round/{i}/history", async Task<Results<Ok<IPlayerResult[]>, NotFound>> (GameStore store, string room, int i, CancellationToken token)
    => await store.Get(room, token) is { } game && i >= 0 && i < game.Rounds.Length
        ? TypedResults.Ok<IPlayerResult[]>([.. game.Rounds[i].Histories.Select(h => h.Result)])
        : TypedResults.NotFound());
api.MapGet("/{room}/round/{i}/history/internal", async Task<Results<Ok<HistoryInfo[]>, NotFound>> (GameStore store, string room, int i, CancellationToken token)
    => await store.Get(room, token) is { } game && i >= 0 && i < game.Rounds.Length
        ? TypedResults.Ok<HistoryInfo[]>([.. game.Rounds[i].Histories])
        : TypedResults.NotFound());

// ユーザーの解答と結果
api.MapPost("/answer", async Task<Results<Ok<AnswerResultType>, ProblemHttpResult>> (HttpContext context, GameStore store, GameAi ai, [FromBody] string input) =>
{
    if (await GetPlayerContext(context, store) is not { User: var user, Game: var game })
    {
        return NotFoundProblem("ゲームが見つかりません");
    }
    if (game is not { CurrentScene: GameScene.QuestionAnswering, Rounds: [.., var round] })
    {
        return BadRequestProblem("解答できるシーンではありません");
    }
    if (string.IsNullOrWhiteSpace(input))
    {
        return BadRequestProblem("解答が必要です");
    }
    if (round.CountAnswers(user.Id) >= game.Config.AnswerLimit)
    {
        return BadRequestProblem("解答回数の上限に達しました");
    }

    var res = input == round.Topic
        ? new AnswerResponse("完全一致", AnswerResultType.Correct)
        : await ai.JudgeAnswer(game.Theme, round, input, context.RequestAborted);
    var roundCount = game.Rounds.Length;
    await UpdateAndAdvance(store, ai, game.Id, g =>
    {
        if (g is not { CurrentScene: GameScene.QuestionAnswering } || g.Rounds.Length != roundCount || g.Rounds[^1].CountAnswers(user.Id) >= g.Config.AnswerLimit)
        {
            return g;
        }
        var updated = g.AddHistory(new(new AnswerResult(user.Id, input, res.Result), res.Reason, DateTime.UtcNow));
        return res.Result == AnswerResultType.Correct
            ? updated.UpdatePlayer(user.Id, p => p with { Points = p.Points + g.Config.CorrectPoint })
            : updated;
    }, CancellationToken.None);
    return TypedResults.Ok(res.Result);
});

// 嘘をついているプレイヤーの推理
api.MapPost("/guess", async Task<Results<Ok, ProblemHttpResult>> (HttpContext context, GameStore store, GameAi ai, [FromBody] Guid target) =>
{
    if (await GetPlayerContext(context, store) is not { User: var user, Game: var game })
    {
        return NotFoundProblem("ゲームが見つかりません");
    }
    // 自分自身を指摘することはできない
    if (user.Id == target)
    {
        return BadRequestProblem("自分自身をライアーとして指摘することはできません");
    }
    if (game.CurrentScene != GameScene.LiarGuess)
    {
        return BadRequestProblem("ライアーを推理できるシーンではありません");
    }

    await UpdateAndAdvance(
        store,
        ai,
        game.Id,
        g => g is { CurrentScene: GameScene.LiarGuess, Rounds: [.., var round] } && round.LiarGuesses.All(l => l.Player != user.Id)
            ? g.UpdateCurrentRound(r => r with { LiarGuesses = r.LiarGuesses.Add(new(user.Id, target)) })
            : g,
        CancellationToken.None);
    return TypedResults.Ok();
});

// プレイヤーのバン
api.MapPost("/ban", async Task<Results<Ok, ProblemHttpResult>> (HttpContext context, GameStore store, GameAi ai, [FromBody] Guid target) =>
{
    if (await GetPlayerContext(context, store) is not { User: var user, Game: var game })
    {
        return NotFoundProblem("ゲームが見つかりません");
    }
    if (user.Id != game.HostId && user.Id != target)
    {
        return ForbiddenProblem("このプレイヤーを退出させる権限がありません");
    }

    game = await UpdateAndAdvance(store, ai, game.Id, g =>
    {
        var players = g.Players.RemoveAll(p => p.Id == target);
        return g with
        {
            Players = players,
            // バンされたプレイヤーのお題が使われていない場合、お題を削除
            Topics = g.Rounds.Any(r => r.Liars.Contains(target)) ? g.Topics : g.Topics.Remove(target),
            HostId = g.HostId == target && !players.IsEmpty ? players[0].Id : g.HostId,
        };
    }, CancellationToken.None);
    await store.RemoveUserRoom(target);
    if (game.Players.IsEmpty)
    {
        await store.Remove(game);
    }
    return TypedResults.Ok();
});

// シーン情報取得
api.MapGet("/scene", async Task<Results<Ok<CurrentScene>, NotFound>> (HttpContext context, GameStore store)
    => await GetPlayerContext(context, store) is { Game: var game }
        ? TypedResults.Ok(ToCurrentScene(game))
        : TypedResults.NotFound());

// シーン情報の購読
api.MapGet("/scene/stream", (HttpContext context, GameStore store, RoomNotifier notifier)
    => TypedResults.ServerSentEvents(StreamScene(GetUser(context), store, notifier, context.RequestAborted)));

// プレイヤーのシーン情報更新
api.MapPost("/scene", async Task<Results<Ok, ProblemHttpResult>> (HttpContext context, GameStore store, [FromBody] GameScene scene) =>
{
    if (await GetPlayerContext(context, store) is not { User: var user, Game: var game })
    {
        return NotFoundProblem("ゲームが見つかりません");
    }
    if (game.Players.FirstOrDefault(p => p.Id == user.Id)?.CurrentScene != scene)
    {
        await store.Update(game.Id, g => g.UpdatePlayer(user.Id, p => p with { CurrentScene = scene }), context.RequestAborted);
    }
    return TypedResults.Ok();
});

// プレイヤー情報取得
api.MapGet("/me", Results<Ok<User>, NotFound> (HttpContext context)
    => GetUser(context) is { } u ? TypedResults.Ok(u) : TypedResults.NotFound());

// ゲームリセット
api.MapPost("/reset", async Task<Results<Ok, NotFound>> (HttpContext context, GameStore store) =>
{
    if (await GetPlayerContext(context, store) is not { Game: var game })
    {
        return TypedResults.NotFound();
    }
    await store.Remove(game, context.RequestAborted);
    return TypedResults.Ok();
});

// 設定取得
api.MapGet("/config", async (HttpContext context, GameStore store)
    => TypedResults.Ok((await GetPlayerContext(context, store))?.Game.Config ?? new()));

// 設定更新
api.MapPost("/config", async Task<Results<Ok, ProblemHttpResult>> (HttpContext context, GameStore store, Config newConfig) =>
{
    if (await GetPlayerContext(context, store) is not { Game: var game })
    {
        return NotFoundProblem("ゲームが見つかりません");
    }
    await store.Update(game.Id, g => g with { Config = newConfig }, context.RequestAborted);
    return TypedResults.Ok();
});

api.MapGet("/image/bot/{room}", async Task<Results<FileContentHttpResult, NotFound>> (GameStore store, string room, CancellationToken token)
    => await store.GetBotImage(room, token) is { } img
        ? TypedResults.File(img, "image/png")
        : TypedResults.NotFound());

#if DEBUG
api.MapGet("/wiki", async (WikipediaSearch wiki, string keyword, CancellationToken token) => await wiki.SearchAsync(keyword, token));
api.MapGet("/search", async (TavilySearch tavily, string keyword, CancellationToken token) => await tavily.SearchAsync(keyword, token));
api.MapGet("/gen-image", async (GameAi ai, string theme, CancellationToken token) => TypedResults.File(await ai.GenerateCharacterImage(theme, token), "image/png"));
#endif

app.MapDefaultEndpoints();
app.MapFallbackToFile("/index.html");
app.Run();

record PlayerContext(User User, Game Game);

// ルーム作成リクエスト
record CreateRoomRequest(string Name, string Aikotoba, string Theme);

// ルーム参加リクエスト
record JoinRoomRequest(string Name, string Aikotoba);

record CurrentScene(string Id, string Aikotoba, string Theme, GameScene Scene, int Round, Guid HostId, Player[] Players, ISceneInfo Info);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(EmptySceneInfo), "Empty")]
[JsonDerivedType(typeof(WaitRoundSceneInfo), "WaitRound")]
[JsonDerivedType(typeof(TopicSelectingSceneInfo), "TopicSelecting")]
[JsonDerivedType(typeof(QuestionAnsweringSceneInfo), "QuestionAnswering")]
[JsonDerivedType(typeof(LiarGuessSceneInfo), "LiarGuess")]
[JsonDerivedType(typeof(GameEndInfo), "GameEnd")]
interface ISceneInfo;

record WaitRoundSceneInfo(int Waiting) : ISceneInfo;
record TopicSelectingSceneInfo() : ISceneInfo;
record QuestionAnsweringSceneInfo(IPlayerResult[] Histories) : ISceneInfo;
record LiarGuessSceneInfo(string Topic, Guid[] TopicCorrectPlayers, Guid[] GuessedPlayers, HistoryInfo[] Histories) : ISceneInfo;
record RoundResult(string Topic, Guid[] TopicCorrectPlayers, Guid[] LiarPlayers, Guid[] LiarCorrectPlayers);
record GameEndInfo(RoundResult[] Results) : ISceneInfo;
record EmptySceneInfo() : ISceneInfo;
