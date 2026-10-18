using System.Collections.Immutable;
using System.Text.Json.Serialization;
using MessagePack;

namespace Korenan.ApiService;

/// <summary>
/// ゲーム情報
/// </summary>
/// <param name="Id">ゲームID</param>
/// <param name="Aikotoba">合言葉</param>
/// <param name="Theme">テーマ</param>
/// <param name="Players">参加プレイヤー</param>
/// <param name="Topics">お題</param>
/// <param name="Rounds">ラウンド</param>
/// <param name="CurrentScene">現在のシーン</param>
/// <param name="Config">コンフィグ</param>
/// <param name="HostId">ホストのプレイヤーID</param>
[MessagePackObject]
public record Game(
    [property: Key(0)] string Id,
    [property: Key(1)] string Aikotoba,
    [property: Key(2)] string Theme,
    [property: Key(3)] ImmutableArray<Player> Players,
    [property: Key(4)] ImmutableDictionary<Guid, string> Topics,
    [property: Key(5)] ImmutableArray<Round> Rounds,
    [property: Key(6)] GameScene CurrentScene,
    [property: Key(7)] Config Config,
    [property: Key(8)] Guid HostId);

public enum GameScene
{
    /// <summary>
    /// お題登録フェーズ
    /// </summary>
    RegisterTopic,

    /// <summary>
    /// ラウンド開始を待つフェーズ
    /// </summary>
    WaitRoundStart,

    /// <summary>
    /// お題を抽選するフェーズ
    /// </summary>
    TopicSelecting,

    /// <summary>
    /// プレイヤーが質問したり、解答したりするフェーズ
    /// </summary>
    QuestionAnswering,

    /// <summary>
    /// プレイヤーが嘘をついているプレイヤーを推理するフェーズ
    /// </summary>
    LiarGuess,

    /// <summary>
    /// すべてのラウンドが終了し、ゲームの結果を表示するフェーズ
    /// </summary>
    GameEnd,
};

/// <summary>
/// プレイヤー情報
/// </summary>
/// <param name="Id">プレイヤーID</param>
/// <param name="Name">プレイヤー名</param>
/// <param name="CurrentScene">プレイヤーの現在のシーン</param>
/// <param name="Points">プレイヤーのポイント</param>
[MessagePackObject]
public record Player(
    [property: Key(0)] Guid Id,
    [property: Key(1)] string Name,
    [property: Key(2)] GameScene CurrentScene = GameScene.RegisterTopic,
    [property: Key(3)] int Points = 0);

/// <summary>
/// ラウンド情報
/// </summary>
/// <param name="Topic">お題</param>
/// <param name="TopicInfo">お題の情報</param>
/// <param name="Liars">ライアープレイヤー</param>
/// <param name="Histories">履歴</param>
/// <param name="LiarGuesses">ライアープレイヤーの推理</param>
[MessagePackObject]
public record Round(
    [property: Key(0)] string Topic,
    [property: Key(1)] string TopicInfo,
    [property: Key(2)] ImmutableArray<Guid> Liars,
    [property: Key(3)] ImmutableArray<HistoryInfo> Histories,
    [property: Key(4)] ImmutableArray<LiarGuess> LiarGuesses);

/// <summary>
/// ライアープレイヤーを推理する対象
/// </summary>
/// <param name="Player">推理したプレイヤー</param>
/// <param name="Target">ライアープレイヤー</param>
[MessagePackObject]
public record LiarGuess([property: Key(0)] Guid Player, [property: Key(1)] Guid Target);

/// <summary>
/// 履歴情報
/// </summary>
/// <param name="Result">質問もしくは解答の結果</param>
/// <param name="Reason">結果の判定理由</param>
/// <param name="PostedAt">投稿時間</param>
[MessagePackObject]
public record HistoryInfo([property: Key(0)] IPlayerResult Result, [property: Key(1)] string Reason, [property: Key(2)] DateTime PostedAt);

/// <summary>
/// 結果のインターフェース
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(QuestionResult), "Question")]
[JsonDerivedType(typeof(AnswerResult), "Answer")]
[Union(0, typeof(QuestionResult))]
[Union(1, typeof(AnswerResult))]
public interface IPlayerResult
{
    /// <summary>
    /// プレイヤーID
    /// </summary>
    Guid Player { get; }
}

/// <summary>
/// 質問の結果
/// </summary>
/// <param name="Player">質問したプレイヤー</param>
/// <param name="Question">質問内容</param>
/// <param name="Result">結果</param>
[MessagePackObject]
public record QuestionResult([property: Key(0)] Guid Player, [property: Key(1)] string Question, [property: Key(2)] QuestionResultType Result) : IPlayerResult;

/// <summary>
/// 質問の結果タイプ
/// </summary>
public enum QuestionResultType
{
    /// <summary>
    /// はい
    /// </summary>
    Yes,

    /// <summary>
    /// いいえ
    /// </summary>
    No,

    /// <summary>
    /// 不明
    /// </summary>
    Unanswerable
}

/// <summary>
/// 解答の結果
/// </summary>
/// <param name="Player">解答したプレイヤー</param>
/// <param name="Answer">プレイヤーの解答</param>
/// <param name="Result">結果</param>
[MessagePackObject]
public record AnswerResult([property: Key(0)] Guid Player, [property: Key(1)] string Answer, [property: Key(2)] AnswerResultType Result) : IPlayerResult;

/// <summary>
/// 解答の結果タイプ
/// </summary>
public enum AnswerResultType
{
    /// <summary>
    /// 正解
    /// </summary>
    Correct,

    /// <summary>
    /// より具体的な解答を求める
    /// </summary>
    MoreSpecific,

    /// <summary>
    /// 不正解
    /// </summary>
    Incorrect
}

/// <summary>
/// ゲームの設定
/// </summary>
/// <param name="QuestionLimit">プレイヤーのラウンド毎の質問上限</param>
/// <param name="AnswerLimit">プレイヤーのラウンド毎の解答上限</param>
/// <param name="CorrectPoint">正解時のポイント</param>
/// <param name="LiarPoint">ライアープレイヤーを当てたときのポイント</param>
/// <param name="NoCorrectPoint">正解者がいなかったときのポイント</param>
/// <param name="InactivityThresholdMinutes">AI自動質問の非活動閾値（分）</param>
/// <param name="AiQuestionThreshold">AI質問開始の閾値係数</param>
[MessagePackObject]
public record Config(
    [property: Key(0)] int QuestionLimit = 8,
    [property: Key(1)] int AnswerLimit = 3,
    [property: Key(2)] int CorrectPoint = 20,
    [property: Key(3)] int LiarPoint = 30,
    [property: Key(4)] int NoCorrectPoint = -10,
    [property: Key(5)] float InactivityThresholdMinutes = 0.5f,
    [property: Key(6)] float AiQuestionThreshold = 0.5f);

static class GameExtensions
{
    /// <summary>
    /// ボットのプレイヤーID
    /// </summary>
    public static readonly Guid BotId = Guid.Empty;

    /// <summary>
    /// 正解者を取得する
    /// </summary>
    /// <param name="round">対象のラウンド</param>
    /// <returns>正解者一覧</returns>
    public static IEnumerable<Guid> GetCorrectPlayers(this Round round)
        => round.Histories
            .Select(x => x.Result)
            .OfType<AnswerResult>()
            .Where(x => x.Result == AnswerResultType.Correct)
            .Select(x => x.Player)
            .Distinct();

    /// <summary>
    /// プレイヤーの質問数を取得する
    /// </summary>
    public static int CountQuestions(this Round round, Guid player)
        => round.Histories.Select(h => h.Result).OfType<QuestionResult>().Count(h => h.Player == player);

    /// <summary>
    /// プレイヤーの解答数を取得する
    /// </summary>
    public static int CountAnswers(this Round round, Guid player)
        => round.Histories.Select(h => h.Result).OfType<AnswerResult>().Count(h => h.Player == player);

    /// <summary>
    /// まだ出題されていないお題を取得する
    /// </summary>
    public static string[] GetRemainingTopics(this Game game)
        => [.. game.Topics.Values.Distinct().Except(game.Rounds.Select(r => r.Topic))];

    /// <summary>
    /// ゲームとすべてのプレイヤーのシーンを変更する
    /// </summary>
    public static Game WithScene(this Game game, GameScene scene)
        => game with
        {
            CurrentScene = scene,
            Players = [.. game.Players.Select(p => p with { CurrentScene = scene })],
        };

    /// <summary>
    /// 指定したプレイヤーを更新する
    /// </summary>
    public static Game UpdatePlayer(this Game game, Guid id, Func<Player, Player> update)
        => game with { Players = [.. game.Players.Select(p => p.Id == id ? update(p) : p)] };

    /// <summary>
    /// 現在のラウンドを更新する
    /// </summary>
    public static Game UpdateCurrentRound(this Game game, Func<Round, Round> update)
        => game with { Rounds = game.Rounds.SetItem(game.Rounds.Length - 1, update(game.Rounds[^1])) };

    /// <summary>
    /// 現在のラウンドに履歴を追加する
    /// </summary>
    public static Game AddHistory(this Game game, HistoryInfo history)
        => game.UpdateCurrentRound(r => r with { Histories = r.Histories.Add(history) });
}
