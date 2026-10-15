namespace Korenan.ApiService;

/// <summary>
/// 生成AIのプロバイダ
/// </summary>
public enum AiProvider
{
    /// <summary>
    /// Google Gemini
    /// </summary>
    Gemini,

    /// <summary>
    /// OpenAI もしくは OpenAI 互換API（Azure OpenAI を含む）
    /// </summary>
    OpenAI,
}

/// <summary>
/// 生成AIの設定
/// </summary>
public sealed record AiOptions
{
    public AiProvider Provider { get; init; } = AiProvider.Gemini;

    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// OpenAI 互換APIのエンドポイント。未指定の場合は OpenAI の既定エンドポイント
    /// </summary>
    public Uri? Endpoint { get; init; }

    public AiModelOptions Models { get; init; } = new();

    /// <summary>
    /// 未指定のモデルIDをプロバイダ毎の既定値で補完する
    /// </summary>
    public AiModelOptions ResolveModels()
    {
        var defaults = Provider switch
        {
            AiProvider.OpenAI => new AiModelOptions { Judge = "gpt-5-mini", Light = "gpt-5-nano", Bot = "gpt-5-mini", Image = "gpt-image-1" },
            _ => new AiModelOptions { Judge = "gemini-2.5-flash", Light = "gemini-2.5-flash-lite", Bot = "gemini-2.5-flash", Image = "gemini-2.5-flash-image" },
        };
        return new()
        {
            Judge = string.IsNullOrEmpty(Models.Judge) ? defaults.Judge : Models.Judge,
            Light = string.IsNullOrEmpty(Models.Light) ? defaults.Light : Models.Light,
            Bot = string.IsNullOrEmpty(Models.Bot) ? defaults.Bot : Models.Bot,
            Image = Models.Image ?? defaults.Image,
        };
    }
}

/// <summary>
/// 用途別のモデルID
/// </summary>
public sealed record AiModelOptions
{
    /// <summary>
    /// 質問への回答や解答の正誤判定に使うモデル
    /// </summary>
    public string Judge { get; init; } = string.Empty;

    /// <summary>
    /// キーワード生成や要約などの軽量な処理に使うモデル
    /// </summary>
    public string Light { get; init; } = string.Empty;

    /// <summary>
    /// ボットの質問生成に使うモデル
    /// </summary>
    public string Bot { get; init; } = string.Empty;

    /// <summary>
    /// キャラクター画像の生成に使うモデル。未指定の場合はプロバイダの既定値、空文字の場合は画像生成を行わない
    /// </summary>
    public string? Image { get; init; }
}

/// <summary>
/// 用途別チャットクライアントのキー
/// </summary>
public static class AiModel
{
    public const string Judge = "judge";
    public const string Light = "light";
    public const string Bot = "bot";
}

/// <summary>
/// 生成AIのテレメトリ設定
/// </summary>
public static class AiTelemetry
{
    public const string SourceName = "Korenan.AI";
}
