using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace Korenan.ApiService;

static class KernelExtensions
{
    public static IKernelBuilderPlugins AddFromFunctions(this IKernelBuilderPlugins builder)
    {
        builder.AddFromFunctions("korenan", [
            CreateConcatQuestion(),
            CreateGetRelationKeywords(),
            CreateGetAnswer(),
            CreateGenQuestion(),
            CreateIsAnswer(),
            CreateSummary(),
        ]);
        return builder;
    }

    private static KernelFunction CreateConcatQuestion()
    {
        // 質問文を整形するプロンプト
        var prompt = new PromptTemplateConfig("""
            あなたは文章の校正を行うアシスタントです。
            与えられたユーザーの入力と対象をつなげて、対象に対する質問文を出力してください。
            出力は質問文のみとし、余計な説明は加えないでください。

            ## ユーザーの入力
            {{$input}}

            ## 対象
            {{$target}}

            ### 例
            「ユーザーの入力」と「対象」を元に生成される質問文を「出力」に示します。

            * ユーザーの入力: 「首都ですか？」
            * 対象: 「東京」
            * 出力: 「東京は首都ですか？」

            * ユーザーの入力: 「生き物？」
            * 対象: 「犬」
            * 出力: 「犬は生き物ですか？」

            * ユーザーの入力: 「それは生き物ですか」
            * 対象: 「日本」
            * 出力: 「日本は生き物ですか？」
            """)
        {
            Name = "question",
            Description = "「ユーザーの入力」と「対象」から対象に対する質問文を生成する",
            InputVariables = [
                new() { Name = "input", IsRequired = true, Description = "ユーザーの入力" },
                new() { Name = "target", IsRequired = true, Description = "対象" }
            ],
        };
        return KernelFunctionFactory.CreateFromPrompt(prompt);
    }

    private static KernelFunction CreateGetRelationKeywords()
    {
        var prompt = new PromptTemplateConfig("""
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
        return KernelFunctionFactory.CreateFromPrompt(prompt);
    }

    private static KernelFunction CreateGetAnswer()
    {
        var prompt = new PromptTemplateConfig("""
        次の参考情報を基にして、ユーザーの質問に回答してください。

        ## テーマ
        {{$theme}}

        ## 対象
        {{$target}}

        ## 参考情報
        {{ $targetInfo }}
        {{ search $keywords }}

        ## ユーザーの質問
        {{ korenan.question input=$input target=$target }}

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
        """)
        {
            Name = "answer",
            InputVariables = [new() { Name = "theme" }, new() { Name = "target" }, new() { Name = "targetInfo" }, new() { Name = "input" }, new() { Name = "keywords" }],
        };
        return KernelFunctionFactory.CreateFromPrompt(prompt);
    }

    private static KernelFunction CreateGenQuestion()
    {
        var prompt = new PromptTemplateConfig("""
            あなたはゲームの参加者です。お題を特定するために必要な情報について、「はい」「いいえ」で答えられる質問を1つだけ生成して。

            ## テーマ
            {{$theme}}

            ## お題
            {{$topic}}

            ## お題の情報
            {{$topicInfo}}

            ## 既存の質問履歴
            {{$existingQuestions}}

            ## 作成する質問文の指針
            * 既存の質問履歴と重複禁止。
              * 新しい視点からの質問を考えて。
              * お題固有の情報に関する質問ではなく、同じテーマの他のお題にも適用できる一般的な質問にして。
            * 対象の特徴や性質について探る質問を心がけて。
            * 質問文は自然で分かりやすい表現にして。
            * 質問文にお題の文言を入れることは禁止。
            * 質問文に固有名詞を入れることは{{$propernoun}}。
            * 「{{$yesno}}」と答えられる質問にして。

            ## 出力
            質問文のみを出力して。
            「この{{$topic}}は、～」の形式で始まり、「〜ですか？」の形式で終わるようにして。
            * 既存の質問履歴と重複禁止。
            """)
        {
            Name = "generateQuestion",
            InputVariables = [
                new() { Name = "theme", IsRequired = true },
                new() { Name = "topic", IsRequired = true },
                new() { Name = "topicInfo", IsRequired = true },
                new() { Name = "existingQuestions", IsRequired = true },
                new() { Name = "yesno", IsRequired = true },
                new() { Name = "propernoun", IsRequired = true }
            ]
        };
        return KernelFunctionFactory.CreateFromPrompt(prompt);
    }

    private static KernelFunction CreateIsAnswer()
    {
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
        """)
        {
            Name = "is_answer",
            InputVariables = [new() { Name = "correct" }, new() { Name = "answer" }, new() { Name = "correctInfo" }, new() { Name = "keywords" }],
        };
        return KernelFunctionFactory.CreateFromPrompt(prompt);
    }

    private static KernelFunction CreateSummary()
    {
        var prompt = new PromptTemplateConfig("""
        あなたは情報の整理を行うアシスタントです。
        入力されたテーマのお題に関する情報を整理してまとめてください。

        ## テーマ
        {{$theme}}

        ## お題
        {{$topic}}

        ## お題に関する情報
        {{$topicInfo}}

        ## 出力
        整理した情報のみを出力してください。
        """)
        {
            Name = "summary",
            Description = "情報整理",
            InputVariables = [
                new() { Name = "theme", IsRequired = true, Description = "テーマ" },
                new() { Name = "topic", IsRequired = true, Description = "お題" },
                new() { Name = "topicInfo", IsRequired = true, Description = "お題に関する情報" },
            ],
        };
        return KernelFunctionFactory.CreateFromPrompt(prompt);
    }

    public static async Task<string> ConcatQuestion(this Kernel kernel, string target, string question)
        => await kernel.InvokeAsync<string>("korenan", "question", new() { ["target"] = target, ["input"] = question }) ?? string.Empty;

    public static async Task<string> GetRelationKeywords(this Kernel kernel, string correct, string target, string theme)
        => await kernel.InvokeAsync<string>("korenan", "keywords", new() { ["correct"] = correct, ["target"] = target, ["theme"] = theme }) ?? string.Empty;

    public static async Task<QuestionResponse> GetAwnser(this Kernel kernel, string theme, Round round, string input, string keywords)
    {
        var result = await kernel.InvokeAsync("korenan", "answer", new() { ["theme"] = theme, ["target"] = round.Topic, ["targetInfo"] = round.TopicInfo, ["input"] = input, ["keywords"] = keywords });
        return result.GetFromJson<QuestionResponse>();
    }

    public static async Task<string> GenQuestion(this Kernel kernel, string theme, Round round, bool yesno, bool propernoun)
        => await kernel.InvokeAsync<string>(
            "korenan",
            "generateQuestion",
            new()
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
            }) ?? string.Empty;

    public static async Task<AnswerResponse> IsAnswer(this Kernel kernel, Round round, string input, string keywords)
    {
        var result = await kernel.InvokeAsync("korenan", "is_answer", new() { ["correct"] = round.Topic, ["answer"] = input, ["correctInfo"] = round.TopicInfo, ["keywords"] = keywords });
        return result.GetFromJson<AnswerResponse>();
    }

    public static async Task<string> Summary(this Kernel kernel, string theme, string topic, string topicInfo)
        => await kernel.InvokeAsync<string>("korenan", "summary", new() { ["theme"] = theme, ["topic"] = topic, ["topicInfo"] = topicInfo }) ?? string.Empty;

    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
        }
    };

    private static T GetFromJson<T>(this FunctionResult result)
    {
        var json = result.GetValue<string>()!.Trim('`', '\n');
        if (!json.StartsWith('{'))
        {
            json = json[json.IndexOf('{')..];
        }
        return JsonSerializer.Deserialize<T>(json, jsonOptions)!;
    }
}