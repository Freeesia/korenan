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
[MessagePackObject]
public record Game(
    [property: Key(0)] string Id,
    [property: Key(1)] string Aikotoba,
    [property: Key(2)] string Theme,
    [property: Key(3)] List<Player> Players,
    [property: Key(4)] Dictionary<Guid, string> Topics,
    [property: Key(5)] List<Round> Rounds,
    [property: Key(6)] GameScene CurrentScene,
    [property: Key(7)] Config Config);

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
/// <param name="Topic">登録したお題</param>
[MessagePackObject]
public record Player([property: Key(0)] Guid Id, [property: Key(1)] string Name)
{
    /// <summary>
    /// プレイヤーの現在のシーン
    /// </summary>
    [Key(2)]
    public GameScene CurrentScene { get; set; }

    /// <summary>
    /// プレイヤーのポイント
    /// </summary>
    [Key(3)]
    public int Points { get; set; }
}

/// <summary>
/// ラウンド情報
/// </summary>
/// <param name="Topic">お題</param>
/// <param name="TopicInfo">お題の情報</param>
/// <param name="Liars">ライアープレイヤー</param>
/// <param name="Histories">履歴</param>
/// <param name="LiarGuesses">ライアープレイヤーの推理</param>
[MessagePackObject]
public record Round([property: Key(0)] string Topic, [property: Key(1)] string TopicInfo, [property: Key(2)] Guid[] Liars, [property: Key(3)] List<HistoryInfo> Histories, [property: Key(4)] List<LiarGuess> LiarGuesses);

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
/// プレイヤー行動の結果タイプ
/// </summary>
public enum PlayerResultType
{
    /// <summary>
    /// 質問
    /// </summary>
    Question,

    /// <summary>
    /// 解答
    /// </summary>
    Answer,
}

/// <summary>
/// 結果のインターフェース
/// </summary>
[JsonDerivedType(typeof(QuestionResult))]
[JsonDerivedType(typeof(AnswerResult))]
[Union(0, typeof(QuestionResult))]
[Union(1, typeof(AnswerResult))]
public interface IPlayerResult
{
    /// <summary>
    /// 結果のタイプ
    /// </summary>
    PlayerResultType Type { get; }

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
public record QuestionResult([property: Key(0)] Guid Player, [property: Key(1)] string Question, [property: Key(2)] QuestionResultType Result) : IPlayerResult
{
    [IgnoreMember]
    public PlayerResultType Type => PlayerResultType.Question;
}

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
public record AnswerResult([property: Key(0)] Guid Player, [property: Key(1)] string Answer, [property: Key(2)] AnswerResultType Result) : IPlayerResult
{
    [IgnoreMember]
    public PlayerResultType Type => PlayerResultType.Answer;
}

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
    /// 正解者を取得する
    /// </summary>
    /// <param name="round">対象のラウンド</param>
    /// <returns>正解者一覧</returns>
    public static IEnumerable<Guid> GetCorrectPlayers(this Round round)
        => round.Histories
            .Where(x => x.Result is AnswerResult answer && answer.Result == AnswerResultType.Correct)
            .Select(x => x.Result.Player);
}
