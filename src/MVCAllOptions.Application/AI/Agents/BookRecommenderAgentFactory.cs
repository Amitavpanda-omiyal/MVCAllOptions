using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat; // provides OpenAIChatClientExtensions.AsAIAgent

namespace MVCAllOptions.AI.Agents;

/// <summary>
/// Creates the BookRecommenderAgent — a higher-level agent that calls
/// BookCatalogAgent as a nested tool (agent-as-a-tool pattern).
///
/// This demonstrates the pattern:
///   outer agent → inner agent.AsAIFunction() → actual tool calls
/// </summary>
public static class BookRecommenderAgentFactory
{
    public static AIAgent Create(IConfiguration config, AIAgent catalogAgent)
    {
        var chatClient = ChatClientFactory.Create(config);

        // Key pattern: AsAIFunction() wraps the inner agent as a callable tool
        AIFunction catalogTool = catalogAgent.AsAIFunction(
            options: new AIFunctionFactoryOptions
            {
                Name        = "query_book_catalogue",
                Description = "Queries the bookstore catalogue to search, filter, and retrieve book information.",
            });

        return chatClient.AsAIAgent(
            instructions: """
                You are a personalised book recommender for MVCAllOptions Bookstore.
                Your goal is to understand what the customer wants and recommend 2-3 books they will love.
                
                Workflow:
                1. Ask the customer about their reading preferences (genre, mood, budget, favourite authors).
                2. Use the query_book_catalogue tool to search the catalogue based on their preferences.
                3. Present your top 2-3 recommendations with a brief personalised reason for each.
                4. Offer to refine the recommendations if they want something different.
                
                Be warm, enthusiastic about books, and always explain WHY a book is a good match.
                """,
            name:        "BookRecommenderAgent",
            description: "Provides personalised book recommendations using the catalogue agent.",
            tools:       [catalogTool]);
    }
}
