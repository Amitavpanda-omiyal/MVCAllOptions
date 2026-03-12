using System;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace MVCAllOptions.AI;

/// <summary>
/// Creates an OpenRouter-backed ChatClient.
/// OpenRouter exposes an OpenAI-compatible API so the standard OpenAI package works directly.
///
/// Configure in appsettings.json:
///   "OpenRouter": {
///     "ApiKey":  "sk-or-v1-...",
///     "Model":   "openai/gpt-4o-mini"   (or any model from openrouter.ai/models)
///   }
/// </summary>
internal static class ChatClientFactory
{
    private const string OpenRouterEndpoint = "https://openrouter.ai/api/v1";

    public static ChatClient Create(IConfiguration config)
    {
        var apiKey = config["OpenRouter:ApiKey"]
            ?? throw new InvalidOperationException(
                "OpenRouter:ApiKey is required. Add it to appsettings.json or set the OPENROUTER__APIKEY env var.");

        var model = config["OpenRouter:Model"] ?? "openai/gpt-4o-mini";

        return new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(OpenRouterEndpoint) })
            .GetChatClient(model);
    }
}
