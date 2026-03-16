using System;
using System.ClientModel;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using Volo.AIManagement.Factory;
using Volo.Abp.DependencyInjection;

namespace MVCAllOptions.AI;

public class BookAwareOpenAIChatClientFactory : IChatClientFactory, ITransientDependency
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookAwareOpenAIChatClientFactory> _logger;

    public BookAwareOpenAIChatClientFactory(
        IServiceScopeFactory scopeFactory,
        ILogger<BookAwareOpenAIChatClientFactory> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Provider => "OpenAI";

    public Task<IChatClient> CreateAsync(ChatClientCreationConfiguration configuration)
    {
        _logger.LogInformation(
            "BookAwareOpenAIChatClientFactory.CreateAsync called for workspace '{WorkspaceName}', provider '{Provider}', model '{Model}'",
            configuration.Name, configuration.Provider, configuration.ModelName);

        var client = new OpenAIClient(
            new ApiKeyCredential(configuration.ApiKey
                ?? throw new ArgumentNullException(nameof(configuration.ApiKey))),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(configuration.ApiBaseUrl ?? "https://api.openai.com/v1")
            });

        IChatClient innerClient = client
            .GetChatClient(configuration.ModelName)
            .AsIChatClient();

        // For OpenAIRAGWorkspace, inject book catalogue context
        if (string.Equals(configuration.Name, "OpenAIRAGWorkspace", StringComparison.Ordinal))
        {
            innerClient = new BookContextChatClient(innerClient, _scopeFactory);
        }

        // FunctionInvokingChatClient MUST be the outermost wrapper —
        // ABP's AIManagementChatClient.PrepareRagToolsAsync requires it
        IChatClient chatClient = new FunctionInvokingChatClient(innerClient);

        return Task.FromResult(chatClient);
    }
}
