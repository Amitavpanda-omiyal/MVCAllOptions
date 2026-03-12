using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using MVCAllOptions.AI.Tools;
using OpenAI.Chat; // provides OpenAIChatClientExtensions.AsAIAgent

namespace MVCAllOptions.AI.Agents;

/// <summary>
/// Creates the BookCatalogAgent — a tool-calling agent that can search,
/// filter and surface information from the bookstore catalogue.
/// Demonstrates the basic AIAgent + AIFunctionFactory pattern.
/// </summary>
public static class BookCatalogAgentFactory
{
    public static AIAgent Create(IConfiguration config)
    {
        var chatClient = ChatClientFactory.Create(config);

        return chatClient.AsAIAgent(
            instructions: """
                You are a knowledgeable and friendly bookstore assistant for MVCAllOptions Bookstore.
                You help customers find books they will love.
                
                You have access to our full catalogue. When answering:
                - Always search the catalogue before responding about specific books.
                - Be concise but informative.
                - Mention the price and genre when recommending a book.
                - If asked for recommendations, ask about preferred genres or price range first.
                """,
            name:        "BookCatalogAgent",
            description: "Searches and retrieves book information from the MVCAllOptions Bookstore catalogue.",
            tools: [
                AIFunctionFactory.Create(BookstoreTools.SearchBooks),
                AIFunctionFactory.Create(BookstoreTools.GetBooksByGenre),
                AIFunctionFactory.Create(BookstoreTools.GetBookDetails),
                AIFunctionFactory.Create(BookstoreTools.GetAllBooks),
                AIFunctionFactory.Create(BookstoreTools.GetBooksByPriceRange),
                AIFunctionFactory.Create(BookstoreTools.ListGenres),
            ]);
    }
}
