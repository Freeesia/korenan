
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.SemanticKernel;
using StackExchange.Redis;

namespace Korenan.ApiService;

public class BotService(ILogger<BotService> logger, Kernel kernel, IBufferDistributedCache cache, IConnectionMultiplexer redis) : BackgroundService
{
    private readonly ILogger<BotService> logger = logger;
    private readonly Kernel kernel = kernel;
    private readonly IBufferDistributedCache cache = cache;
    private readonly IConnectionMultiplexer redis = redis;
    private readonly PeriodicTimer timer = new(TimeSpan.FromSeconds(10));
    private readonly Random random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            this.logger.LogInformation("BotService is running at: {time}", DateTime.Now);
            try
            {
                await CheckAndPostQuestionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error occurred while checking and posting questions");
            }
        }
    }

    private async Task CheckAndPostQuestionsAsync(CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints().First());

        // アクティブなゲームを取得
        var gameKeys = server.Keys(pattern: "game/room/*").ToArray();

        foreach (var gameKey in gameKeys)
        {
            try
            {
                var gameId = gameKey.ToString().Split('/').Last();
                var game = await cache.Get<Game>(gameKey.ToString(), cancellationToken);

                if (game is not { CurrentScene: GameScene.QuestionAnswering })
                    continue;

                var currentRound = game.Rounds.LastOrDefault();
                if (currentRound == null)
                    continue;

                var last = currentRound.Histories.LastOrDefault();
                if (last is { Result: AnswerResult { Result: AnswerResultType.Correct } } or null)
                    continue;

                // 最後の投稿から設定された時間以上経過しているかチェック
                if (DateTime.UtcNow - (last?.PostedAt ?? DateTime.MinValue) < TimeSpan.FromMinutes(game.Config.InactivityThresholdMinutes))
                    continue;

                // AIが質問を生成して投稿
                await GenerateAndPostQuestionAsync(game, currentRound, cancellationToken);

            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing game {GameKey}", gameKey);
            }
        }
    }

    private async Task GenerateAndPostQuestionAsync(Game game, Round round, CancellationToken cancellationToken)
    {
        var questions = round.Histories
            .Select(h => h.Result)
            .OfType<QuestionResult>()
            .ToArray();
        var maxQuestions = game.Config.QuestionLimit * game.Players.Count;
        var playersCount = questions
            .Where(h => h.Player != Guid.Empty)
            .Count();
        var aiCount = questions
            .Where(h => h.Player == Guid.Empty)
            .Count();

        // 後半戦に入るまではAIは質問しない
        if (playersCount < maxQuestions * 0.5)
        {
            this.logger.LogInformation("Not enough player questions yet for game {GameId}, skipping AI question", game.Id);
            return;
        }

        // Botの質問上限に達している場合はスキップ
        if (aiCount >= game.Config.QuestionLimit)
        {
            this.logger.LogInformation("AI has reached question limit for game {GameId}", game.Id);
            return;
        }

        // Botによる質問生成
        var yesno = random.Next(2) == 0;
        // Bot自身の後半の質問かつ全体の質問数の75%が埋まっている場合のみ固有名詞を含む質問を許可
        var propernoun = aiCount > game.Config.QuestionLimit / 2 && playersCount > maxQuestions * 0.75;
        var generatedQuestion = await kernel.GenQuestion(game.Theme, round, yesno, propernoun);

        if (string.IsNullOrEmpty(generatedQuestion))
        {
            this.logger.LogWarning("Failed to generate question for game {GameId}", game.Id);
            return;
        }

        if (generatedQuestion.Contains(round.Topic, StringComparison.OrdinalIgnoreCase))
        {
            this.logger.LogInformation("Generated question is too revealing for game {GameId}, skipping", game.Id);
            return;
        }

        // 生成された質問でAIが回答を得る
        var keywords = await kernel.GetRelationKeywords(round.Topic, generatedQuestion, game.Theme);
        var response = await kernel.GetAwnser(game.Theme, round, generatedQuestion, keywords);

        // 履歴に追加
        await cache.Update<Game>(
            $"game/room/{game.Id}",
            g => g.Rounds.Last().Histories.Add(new(
                    new QuestionResult(Guid.Empty, generatedQuestion, response.Result),
                    response.Reason,
                    DateTime.UtcNow)),
            cancellationToken);
        this.logger.LogInformation("Posted AI question for game {GameId}", game.Id);
    }
}