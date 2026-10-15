using Google.GenAI.Types;
using Microsoft.Extensions.AI;

namespace Korenan.ApiService;

/// <summary>
/// 画像出力に対応した Gemini のチャットモデルで画像を生成・編集する
/// </summary>
public sealed class ChatImageGenerator(IChatClient chatClient) : IImageGenerator
{
    public async Task<ImageGenerationResponse> GenerateAsync(ImageGenerationRequest request, ImageGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var message = new ChatMessage(ChatRole.User, [new TextContent(request.Prompt), .. request.OriginalImages ?? []]);
        var chatOptions = new ChatOptions
        {
            ModelId = options?.ModelId,
            RawRepresentationFactory = _ => new GenerateContentConfig { ResponseModalities = ["TEXT", "IMAGE"] },
        };
        var response = await chatClient.GetResponseAsync(message, chatOptions, cancellationToken);
        var images = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<DataContent>()
            .Where(c => c.HasTopLevelMediaType("image"))
            .ToList<AIContent>();
        return new(images) { RawRepresentation = response };
    }

    public object? GetService(System.Type serviceType, object? serviceKey = null)
        => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : chatClient.GetService(serviceType, serviceKey);

    public void Dispose() => chatClient.Dispose();
}
