using System.Text.Json.Serialization;

namespace korenan.ApiService;

/// <summary>
/// ゲーム情報
/// </summary>
/// <param name="Players">参加プレイヤー</param>
/// <param name="Rounds">ラウンド</param>
/// <param name="CurrentScene">現在のシーン</param>
/// <param name="Config">コンフィグ</param>
record Game(List<Player> Players, List<Round> Rounds, GameScene CurrentScene, Config Config);
enum GameScene
{
    /// <summary>
    /// プレイヤーがトピックを登録するフェーズ
    /// </summary>
    RegistTopic,

    /// <summary>
    /// ラウンド開始を待つフェーズ
    /// </summary>
    WaitRoundStart,

    /// <summary>
    /// プレイヤーが質問したり、解答したりするフェーズ
    /// </summary>
    QuestionAnswering,

    /// <summary>
    /// プレイヤーが嘘をついているプレイヤーを推理するフェーズ
    /// </summary>
    LiarPlayerGuessing,

    /// <summary>
    /// ラウンドの結果を表示するフェーズ
    /// </summary>
    RoundSummary,

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
record Player(Guid Id, string Name, string Topic)
{
    /// <summary>
    /// プレイヤーの現在のシーン
    /// </summary>
    public GameScene CurrentScene { get; set; }

    /// <summary>
    /// プレイヤーのポイント
    /// </summary>
    public int Points { get; set; }
}

/// <summary>
/// ラウンド情報
/// </summary>
/// <param name="Topic">お題</param>
/// <param name="TopicInfo">お題の情報</param>
/// <param name="Histories">履歴</param>
record Round(string Topic, string TopicInfo, List<HistoryInfo> Histories);

/// <summary>
/// 履歴情報
/// </summary>
/// <param name="Result">質問もしくは解答の結果</param>
/// <param name="Reason">結果の判定理由</param>
/// <param name="Prompt">判定に利用したプロンプト</param>
record HistoryInfo(IResult Result, string Reason, string Prompt);

/// <summary>
/// 結果のインターフェース
/// </summary>
[JsonDerivedType(typeof(QuestionResult))]
[JsonDerivedType(typeof(AnswerResult))]
interface IResult;

/// <summary>
/// 質問の結果
/// </summary>
/// <param name="Player">質問したプレイヤー</param>
/// <param name="Question">質問内容</param>
/// <param name="Result">結果</param>
record QuestionResult(Guid Player, string Question, QuestionResultType Result) : IResult;

/// <summary>
/// 質問の結果タイプ
/// </summary>
enum QuestionResultType
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
record AnswerResult(Guid Player, string Answer, AnswerResultType Result) : IResult;

/// <summary>
/// 解答の結果タイプ
/// </summary>
enum AnswerResultType
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
record Config(int QuestionLimit = 5, int AnswerLimit = 3, int CorrectPoint = 20, int LiarPoint = 30);
