using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Korenan.ApiService;

/// <summary>
/// ゲーム進行に必要な生成AIの処理
/// </summary>
public sealed class GameAi(
    [FromKeyedServices(AiModel.Judge)] IChatClient judge,
    [FromKeyedServices(AiModel.Light)] IChatClient light,
    [FromKeyedServices(AiModel.Bot)] IChatClient bot,
    TavilySearch tavily,
    WikipediaSearch wikipedia,
    PromptTemplates prompts,
    IImageGenerator? imageGenerator = null)
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>
    /// お題に関する情報を収集して要約する
    /// </summary>
    public async Task<string> CollectTopicInfo(string theme, string topic, CancellationToken token = default)
    {
        var search = tavily.SearchAsync($"{theme}の{topic}の概要", token);
        var wiki = wikipedia.SearchAsync($"intitle:\"{topic}\" deepcat:\"{theme}\"", token);
        await Task.WhenAll(search, wiki);
        var topicInfo = string.Join(Environment.NewLine, [.. await search, await wiki]);
        return await Complete(light, "summary", new() { ["theme"] = theme, ["topic"] = topic, ["topicInfo"] = topicInfo }, token);
    }

    /// <summary>
    /// お題に対する質問に回答する
    /// </summary>
    public async Task<QuestionResponse> Answer(string theme, Round round, string input, CancellationToken token = default)
    {
        var question = Complete(light, "question", new() { ["input"] = input, ["target"] = round.Topic }, token);
        var search = SearchRelation(theme, round.Topic, input, token);
        await Task.WhenAll(question, search);
        var prompt = prompts.Render("answer", new Dictionary<string, string>
        {
            ["theme"] = theme,
            ["target"] = round.Topic,
            ["targetInfo"] = round.TopicInfo,
            ["search"] = await search,
            ["question"] = await question,
        });
        var response = await judge.GetResponseAsync<QuestionResponse>(prompt, jsonOptions, cancellationToken: token);
        return response.Result;
    }

    /// <summary>
    /// 解答がお題と一致するかを判定する
    /// </summary>
    public async Task<AnswerResponse> JudgeAnswer(string theme, Round round, string input, CancellationToken token = default)
    {
        var answerSearch = tavily.SearchAsync(input, token);
        var answerWiki = wikipedia.SearchAsync(input, token);
        var relation = SearchRelation(theme, round.Topic, input, token);
        await Task.WhenAll(answerSearch, answerWiki, relation);
        var prompt = prompts.Render("isAnswer", new Dictionary<string, string>
        {
            ["correct"] = round.Topic,
            ["answer"] = input,
            ["correctInfo"] = round.TopicInfo,
            ["answerInfo"] = string.Join(Environment.NewLine, [.. await answerSearch, await answerWiki]),
            ["relationInfo"] = await relation,
        });
        var response = await judge.GetResponseAsync<AnswerResponse>(prompt, jsonOptions, cancellationToken: token);
        return response.Result;
    }

    /// <summary>
    /// ボットの質問を生成する
    /// </summary>
    public async Task<string> GenerateQuestion(string theme, Round round, bool yesno, bool propernoun, CancellationToken token = default)
        => await Complete(bot, "generateQuestion", new()
        {
            ["theme"] = theme,
            ["topic"] = round.Topic,
            ["topicInfo"] = round.TopicInfo,
            ["existingQuestions"] = string.Join("\n", round.Histories
                .Select(h => h.Result)
                .OfType<QuestionResult>()
                .Select(q => $"- {q.Question}")),
            ["yesno"] = yesno ? "はい" : "いいえ",
            ["propernoun"] = propernoun ? "許容" : "禁止",
        }, token);

    /// <summary>
    /// テーマに合わせたキャラクター画像を生成する。生成できなかった場合は既定の画像を返す
    /// </summary>
    public async Task<byte[]> GenerateCharacterImage(string theme, CancellationToken token = default)
    {
        var baseImage = GetDefaultCharacterImage();
        if (imageGenerator is null)
        {
            return baseImage;
        }
        var request = new ImageGenerationRequest(
            prompts.Render("characterImage", new Dictionary<string, string> { ["theme"] = theme }),
            [new DataContent(baseImage, "image/png")]);
        var response = await imageGenerator.GenerateAsync(request, new() { MediaType = "image/png" }, token);
        return response.Contents.OfType<DataContent>().FirstOrDefault() is { } image ? image.Data.ToArray() : baseImage;
    }

    /// <summary>
    /// 既定のキャラクター画像を取得する
    /// </summary>
    public static byte[] GetDefaultCharacterImage()
    {
        var resourceName = $"{typeof(GameAi).Namespace}.ai_character01_smile.png";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException($"埋め込みリソース '{resourceName}' が見つかりません。");
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private async Task<string> SearchRelation(string theme, string correct, string target, CancellationToken token)
    {
        var keywords = await Complete(light, "keywords", new() { ["theme"] = theme, ["correct"] = correct, ["target"] = target }, token);
        return string.Join(Environment.NewLine, await tavily.SearchAsync(keywords, token));
    }

    private async Task<string> Complete(IChatClient client, string promptName, Dictionary<string, string> values, CancellationToken token)
    {
        var response = await client.GetResponseAsync(prompts.Render(promptName, values), cancellationToken: token);
        return response.Text.Trim();
    }
}

/// <summary>
/// 質問への回答
/// </summary>
/// <param name="Reason">判断理由</param>
/// <param name="Result">回答</param>
public record QuestionResponse(string Reason, QuestionResultType Result);

/// <summary>
/// 解答の判定結果
/// </summary>
/// <param name="Reason">判断理由</param>
/// <param name="Result">判定結果</param>
public record AnswerResponse(string Reason, AnswerResultType Result);
