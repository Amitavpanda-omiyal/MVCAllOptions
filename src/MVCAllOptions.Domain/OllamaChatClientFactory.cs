using Microsoft.Extensions.AI;
using OllamaSharp;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Volo.AIManagement.Workspaces;
using Volo.AIManagement.Factory;
using Volo.Abp.DependencyInjection;

namespace MVCAllOptions;

public class OllamaChatClientFactory : IChatClientFactory, ITransientDependency
{
    public string Provider => "Ollama";

    public Task<IChatClient> CreateAsync(ChatClientCreationConfiguration configuration)
    {
        var baseUrl = configuration.ApiBaseUrl ?? "http://localhost:11434";
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(10)
        };
        var client = new OllamaApiClient(httpClient, configuration.ModelName);

        return Task.FromResult<IChatClient>(client);
    }
}