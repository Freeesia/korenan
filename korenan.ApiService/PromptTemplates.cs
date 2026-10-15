using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace Korenan.ApiService;

/// <summary>
/// 埋め込みリソースの Prompts/*.md を読み込み、{{name}} を置換してプロンプトを生成する
/// </summary>
public sealed class PromptTemplates
{
    private readonly ConcurrentDictionary<string, string> templates = new();

    public string Render(string name, IReadOnlyDictionary<string, string> values)
    {
        var template = templates.GetOrAdd(name, Load);
        var builder = new StringBuilder(template);
        foreach (var (key, value) in values)
        {
            builder.Replace($"{{{{{key}}}}}", value);
        }
        return builder.ToString();
    }

    private static string Load(string name)
    {
        var resourceName = $"{typeof(PromptTemplates).Namespace}.Prompts.{name}.md";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException($"プロンプト '{resourceName}' が見つかりません。");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
