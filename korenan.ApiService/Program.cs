using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using GoogleTrendsApi;
using korenan.ApiService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using GoogleTrends = GoogleTrendsApi.Api;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

var (modelId, apiKey, bingKey) = builder.Configuration.GetSection(nameof(SemanticKernelOptions)).Get<SemanticKernelOptions>()!;
var kernelBuikder = builder.Services.AddKernel()
    .AddGoogleAIGeminiChatCompletion(modelId, apiKey)
    .AddGoogleAIEmbeddingGeneration(modelId, apiKey);
builder.Services
    .AddHttpClient()
    .ConfigureHttpJsonOptions(op => op.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddSingleton<IWebSearchEngineConnector>(sp => new BingConnector(bingKey, sp.GetRequiredService<HttpClient>(), loggerFactory: sp.GetService<ILoggerFactory>()))
    .AddSingleton(sp => KernelPluginFactory.CreateFromType<WebSearchEnginePlugin>("search", sp))
    .AddSingleton(sp => KernelPluginFactory.CreateFromType<TimePlugin>("time", serviceProvider: sp))
    .AddSingleton(sp => KernelPluginFactory.CreateFromType<WikipediaPlugin>("wiki", serviceProvider: sp));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};
var quiz = new Quiz();

var geminiSettings = new GeminiPromptExecutionSettings()
{
    SafetySettings =
    [
        new(GeminiSafetyCategory.Harassment, GeminiSafetyThreshold.BlockNone),
        new(GeminiSafetyCategory.DangerousContent, GeminiSafetyThreshold.BlockNone),
        new(new("HARM_CATEGORY_HATE_SPEECH"), GeminiSafetyThreshold.BlockNone),
    ],
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.MapGet("/wiki", ([FromServices] Kernel kernel, [FromQuery] string keyword) => kernel.InvokeAsync<string>("wiki", "Search", new() { ["query"] = keyword }));

app.MapPost("/target", async ([FromServices] Kernel kernel, [FromBody] string target) =>
{
    quiz.Correct = target;
    quiz.CorrectInfo = string.Join(Environment.NewLine, [
        await kernel.InvokeAsync<string>("search", "Search", new() { ["query"] = target }),
        await kernel.InvokeAsync<string>("wiki", "Search", new() { ["query"] = target })
        ]);
});

app.MapPost("/question", async ([FromServices] Kernel kernel, [FromBody] string input) =>
{
    var questionPrompt = new PromptTemplateConfig("""
        ���Ȃ��͕��͂̍Z�����s���A�V�X�^���g�ł��B
        �^����ꂽ�ΏۂƎ�����Ȃ��āA�Ώۂɑ΂��鎿�╶���쐬���Ă��������B

        ## �Ώ�
        {{$target}}

        ## ����
        {{$input}}

        ### ��
        * �Ώ�: �u�����v
        * ����: �u��s�ł����H�v
        * �o��: �u�����͎�s�ł����H�v

        * �Ώ�: �u���v
        * ����: �u�������H�v
        * �o��: �u���͐������ł����H�v

        * �Ώ�: �u���{�v
        * ����: �u����͐������ł����v
        * �o��: �u���{�͐������ł����H�v
        """)
    {
        Name = "question",
        Description = "�u�Ώہv�Ɓu����v����Ώۂɑ΂��鎿�╶�𐶐�����",
        InputVariables = [new() { Name = "input", IsRequired = true, Description = "����" }, new() { Name = "target", IsRequired = true, Description = "�Ώ�" }],
    };
    questionPrompt.AddExecutionSettings(geminiSettings);
    var questionFunc = kernel.CreateFunctionFromPrompt(questionPrompt);
    kernel.ImportPluginFromFunctions("question", [questionFunc]);
    var keywords = await kernel.GetRelationKeywords(quiz.Correct, input, geminiSettings);
    var prompt = new PromptTemplateConfig("""
        ���̎Q�l������ɂ��āA���[�U�[�̎���ɉ񓚂��Ă��������B
        
        ## �Q�l���
        {{ $correctInfo }}
        {{ search $keywords }}
              
        ## ���[�U�[�̎���
        {{ question intput=$intput target=$correct }}
        
        ## �񓚂̎w�j
        * �Q�l�����ɁA����ɑ΂��Ė��m�ɍm�肳�����e�������`yes`�Ɖ񓚂��Ă��������B
        * �Q�l�����ɁA����ɑ΂��Ė��m�ɔے肳�����e�������`no`�Ɖ񓚂��Ă��������B
        * �Q�l�����ɁA����ɉ񓚉\�ȏ�񂪊܂܂�Ă��Ȃ��ꍇ��`no`�Ɖ񓚂��Ă��������B
        * ���₪�u�͂��v�u�������v�ŉ񓚂ł��Ȃ��J��������̏ꍇ��`unanswerable`�Ɖ񓚂��Ă��������B
        
        ## �o�̗͂l��
        �ȉ���Json�t�H�[�}�b�g�ɂ��������āA����ɑ΂��ĉ񓚂𓱂��o�������R�ƂƂ��ɉ񓚂��o�͂��Ă��������B
        {
            "reason": "���f�̗��R",
            "result": "`yes`�A`no`�A`unanswerable`�̂����ꂩ�̉�"
        }
        """);
    prompt.AddExecutionSettings(geminiSettings);
    var result = await kernel.InvokeAsync(
        kernel.CreateFunctionFromPrompt(prompt),
        new() { ["correct"] = quiz.Correct, ["input"] = input, ["correctInfo"] = quiz.CorrectInfo, ["keywords"] = keywords });

    var res = result.GetFromJson<QuestionResponse>();
    quiz.Histories.Add(new(new QuestionResult(input, res.Result), res.Reason, result.RenderedPrompt ?? string.Empty));
    return res.Result;
});

app.MapGet("/history", () => quiz.Histories.Select(h => h.Result));
app.MapGet("/history/internal", () => quiz.Histories);

app.MapPost("/answer", async ([FromServices] Kernel kernel, [FromBody] string input) =>
{
    if (input == quiz.Correct)
    {
        quiz.Histories.Add(new(new AnswerResult(input, AnswerResultType.Correct), "���S��v", string.Empty));
        return AnswerResultType.Correct;
    }
    var keywords = await kernel.GetRelationKeywords(quiz.Correct, input, geminiSettings);
    var prompt = new PromptTemplateConfig("""
        ���Ȃ��̓N�C�Y�̏o��҂ł���A���[�U�[����̉񓚂̐���𔻒f������Ƃł��B
        �Q�l������ɂ��āA���[�U�[�̉񓚂��N�C�Y�̐����ɑ΂��ē��ꂩ�ǂ����𔻒f���Ă��������B

        ## ����
        {{ $correct }}

        ## ���[�U�[�̉�
        {{ $answer }}

        ## �����Ɋւ���Q�l���
        {{ $correctInfo }}

        ## �񓚂Ɋւ���Q�l���
        {{ search $answer }}
        {{ wiki.Search $answer }}
        
        ## �����Ɖ񓚂̊֌W���Ɋւ���Q�l���
        {{ search $keywords }}

        ## ���f����яo�͂̎w�j
        1. �����Ɖ񓚂����S��v���Ă���ꍇ��`correct`�Əo�͂��Ă��������B
        2. �����Ɖ񓚂����S��v���Ă��Ȃ����A�\�L�h��ȂǎQ�l�������ɉ񓚂������ƕK�v�\���ɓ���ł���Ɣ��f�ł���ꍇ��`correct`�Əo�͂��Ă��������B
        3. �����Ɖ񓚂���v���Ȃ����A�񓚂������̈ꕔ�ł���A�񓚂������̏\�������𖞂����ꍇ��`correct`�Əo�͂��Ă��������B
        4. �����Ɖ񓚂���v���Ȃ����A�������񓚂̈ꕔ�ł���A�񓚂������̕K�v�����𖞂������A�\�������𖞂����Ȃ��ꍇ��`more_specific`�Əo�͂��Ă��������B
        5. ��L�̂�����ɂ����Ă͂܂�Ȃ��ꍇ��`incorrect`�Əo�͂��Ă��������B

        ## �o�̗͂l��
        �ȉ���Json�t�H�[�}�b�g�ɂ��������āA����ɑ΂��ĉ񓚂𓱂��o�������R�ƂƂ��ɉ񓚂��o�͂��Ă��������B
        {
            "reason": "���f�̗��R",
            "result": "`correct`�A`more_specific`�A`incorrect`�̂����ꂩ�̉�"
        }
        """);
    prompt.AddExecutionSettings(geminiSettings);
    var result = await kernel.InvokeAsync(
        kernel.CreateFunctionFromPrompt(prompt),
        new() { ["correct"] = quiz.Correct, ["answer"] = input, ["correctInfo"] = quiz.CorrectInfo, ["keywords"] = keywords });

    var res = result.GetFromJson<AnswerResponse>();
    quiz.Histories.Add(new(new AnswerResult(input, res.Result), res.Reason, result.RenderedPrompt ?? string.Empty));
    return res.Result;
});

#if DEBUG
app.MapGet("/debug", () => new[]
{
    new HistoryInfo(new QuestionResult("����", QuestionResultType.Yes), "�����͎�s�ł����H", "�����͓��{�̎�s�ł��B"),
    new HistoryInfo(new AnswerResult("����", AnswerResultType.Correct), "���S��v", string.Empty),
    new HistoryInfo(new QuestionResult("��", QuestionResultType.No), "���͐������ł����H", "���͐������ł��B"),
    new HistoryInfo(new AnswerResult("��", AnswerResultType.Correct), "���S��v", string.Empty),
    new HistoryInfo(new QuestionResult("���{", QuestionResultType.Unanswerable), "���{�͐������ł����H", "���{�͍��ł��B"),
    new HistoryInfo(new AnswerResult("���{", AnswerResultType.Correct), "���S��v", string.Empty),
    new HistoryInfo(new QuestionResult("����", QuestionResultType.Yes), "�����͎�s�ł����H", "�����͓��{�̎�s�ł��B"),
    new HistoryInfo(new AnswerResult("����", AnswerResultType.Correct), "���S��v", string.Empty),
});
app.MapGet("/trends/InterestOverTime", () => GoogleTrends.GetInterestOverTimeTyped([string.Empty], GeoId.Japan, DateOptions.LastMonth, GroupOptions.All, hl: "ja"));
app.MapGet("/trends/TrendingSearches", () => GoogleTrends.GetTrendingSearches("japan"));
app.MapGet("/trends/RealtimeSearches", () => GoogleTrends.GetRealtimeSearches("JP"));
app.MapGet("/trends/TopCharts", () => GoogleTrends.GetTopCharts(2020, hl: "ja", geo:"JP"));
app.MapGet("/trends/TodaySearches", () => GoogleTrends.GetTodaySearches(geo:"JP", hl:"ja"));
app.MapGet("/trends/RelatedQueries", () => GoogleTrends.GetRelatedQueries([string.Empty], geo:"JP"));
#endif

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record SemanticKernelOptions(string ModelId, string ApiKey, string BingKey);
record QuestionResponse(string Reason, QuestionResultType Result);
enum QuestionResultType { Yes, No, Unanswerable }
record AnswerResponse(string Reason, AnswerResultType Result);
enum AnswerResultType { Correct, MoreSpecific, Incorrect }
class Quiz
{
    public string Correct { get; set; }
    public string CorrectInfo { get; set; }
    public List<HistoryInfo> Histories { get; } = [];
}
record HistoryInfo(IResult Result, string Reason, string Prompt);
[JsonDerivedType(typeof(QuestionResult))]
[JsonDerivedType(typeof(AnswerResult))]
interface IResult;
record QuestionResult(string Question, QuestionResultType Result) : IResult;
record AnswerResult(string Answer, AnswerResultType Result) : IResult;

static class Extensions
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
        }
    };

    public static async Task<string> GetRelationKeywords(this Kernel kernel, string correct, string target, PromptExecutionSettings settings)
    {
        var ptompt = new PromptTemplateConfig("""
        �Ώۂ�2�̒P��A���͂̊֌W���������G���W���Œ������邽�߂̃L�[���[�h�𐶐����Ă��������B

        ## �Ώ�
        * {{$correct}}
        * {{$target}}

        ### ��
        * �Ώ�: �u�����v�u��s�ł����H�v
        * �L�[���[�h: �u���� ��s ���ǂ����v
        * �Ώ�: �u���v�u�������H�v
        * �L�[���[�h: �u�� ������ ���ǂ����v
        * �Ώ�: �u���{�v�u�������v
        * �L�[���[�h: �u���{ ������ ���ǂ����v

        �L�[���[�h�̓X�y�[�X��؂�Ŏ���ɑ΂���񓚂𓾂邱�Ƃ��ł���悤�Ȍ����G���W���֓n�������o�͂��Ă��������B
        �L�[���[�h�ȊO�̏��͏o�͂��Ȃ��ł��������B
        """)
        {
            Name = "keywords",
            InputVariables = [new() { Name = "correct" }, new() { Name = "target" }],
        };
        ptompt.AddExecutionSettings(settings);
        var keywordsFunc = kernel.CreateFunctionFromPrompt(ptompt);
        return await keywordsFunc.InvokeAsync<string>(kernel, new() { ["correct"] = correct, ["target"] = target }) ?? string.Empty;
    }

    public static T GetFromJson<T>(this FunctionResult result)
    {
        var json = result.GetValue<string>()!.Trim('`', '\n');
        if (!json.StartsWith('{'))
        {
            json = json[json.IndexOf('{')..];
        }
        return JsonSerializer.Deserialize<T>(json, jsonOptions)!;
    }
}