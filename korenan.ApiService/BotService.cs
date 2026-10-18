namespace Korenan.ApiService;

public class BotService(ILogger<BotService> logger, GameAi ai, GameStore store, RoomNotifier notifier) : BackgroundService
{
    private readonly PeriodicTimer timer = new(TimeSpan.FromSeconds(10));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckAndPostQuestionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while checking and posting questions");
            }
        }
    }

    private async Task CheckAndPostQuestionsAsync(CancellationToken cancellationToken)
    {
        foreach (var roomId in notifier.ActiveRooms)
        {
            try
            {
                await ProcessRoomAsync(roomId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing game {GameId}", roomId);
            }
        }
    }

    private async Task ProcessRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        var game = await store.Get(roomId, cancellationToken);
        if (game is null)
        {
            notifier.Deactivate(roomId);
            return;
        }

        if (game is not { CurrentScene: GameScene.QuestionAnswering, Rounds: [.., var round] })
        {
            return;
        }

        var last = round.Histories.LastOrDefault();
        if (last is { Result: AnswerResult { Result: AnswerResultType.Correct } } or null)
        {
            return;
        }

        // 最後の投稿から設定された時間以上経過しているかチェック
        if (DateTime.UtcNow - last.PostedAt < TimeSpan.FromMinutes(game.Config.InactivityThresholdMinutes))
        {
            return;
        }

        await GenerateAndPostQuestionAsync(game, round, cancellationToken);
    }

    private async Task GenerateAndPostQuestionAsync(Game game, Round round, CancellationToken cancellationToken)
    {
        var questions = round.Histories
            .Select(h => h.Result)
            .OfType<QuestionResult>()
            .ToArray();
        var maxQuestions = game.Config.QuestionLimit * game.Players.Length;
        var playersCount = questions.Count(h => h.Player != GameExtensions.BotId);
        var aiCount = questions.Count(h => h.Player == GameExtensions.BotId);

        // 後半戦に入るまではAIは質問しない
        if (playersCount < maxQuestions * game.Config.AiQuestionThreshold)
        {
            logger.LogDebug("Not enough player questions yet for game {GameId}, skipping AI question", game.Id);
            return;
        }

        // Botの質問上限に達している場合はスキップ
        if (aiCount >= game.Config.QuestionLimit)
        {
            logger.LogDebug("AI has reached question limit for game {GameId}", game.Id);
            return;
        }

        var yesno = Random.Shared.Next(2) == 0;
        // Bot自身の後半の質問かつ全体の質問数の75%が埋まっている場合のみ固有名詞を含む質問を許可
        var propernoun = aiCount > game.Config.QuestionLimit / 2 && playersCount > maxQuestions * 0.75;
        var generatedQuestion = await ai.GenerateQuestion(game.Theme, round, yesno, propernoun, cancellationToken);

        if (string.IsNullOrEmpty(generatedQuestion))
        {
            logger.LogWarning("Failed to generate question for game {GameId}", game.Id);
            return;
        }

        if (generatedQuestion.Contains(round.Topic, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Generated question is too revealing for game {GameId}, skipping", game.Id);
            return;
        }

        var response = await ai.Answer(game.Theme, round, generatedQuestion, cancellationToken);

        var roundIndex = game.Rounds.Length;
        await store.Update(
            game.Id,
            g => g.CurrentScene == GameScene.QuestionAnswering && g.Rounds.Length == roundIndex
                ? g.AddHistory(new(new QuestionResult(GameExtensions.BotId, generatedQuestion, response.Result), response.Reason, DateTime.UtcNow))
                : g,
            cancellationToken);
        logger.LogInformation("Posted AI question for game {GameId}", game.Id);
    }
}
