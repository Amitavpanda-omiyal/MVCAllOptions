using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using MEChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MEChatRole   = Microsoft.Extensions.AI.ChatRole;

namespace MVCAllOptions.AI.Workflows.BookEnrichmentWorkflow;

/// <summary>
/// Builds the Book Enrichment Workflow — a 3-agent sequential pipeline using
/// <see cref="AgentWorkflowBuilder.BuildSequential"/>. This natively satisfies
/// the MAF Chat Protocol requirement (List&lt;ChatMessage&gt; + TurnToken),
/// making the workflow fully compatible with both DevUI and in-process execution.
///
/// Pipeline (AIAgent → AIAgent → AIAgent — all communicating via chat messages):
///   User input (natural language or JSON)
///       → BookDataExtractor   — extracts / normalises book metadata to JSON
///       → BookDescriptionWriter — writes the product page description
///       → BookInsightAnalyst    — generates audience, themes, marketing blurb
/// </summary>
public static class BookEnrichmentWorkflowFactory
{
    // ── Agent system prompts ─────────────────────────────────────────────────

    private const string ExtractorPrompt = """
        You are a data extraction specialist for a bookstore cataloguing system.

        Given ANY input text about a book — whether natural language
        (e.g. "Tell me about a sci-fi book called Dune") or structured JSON
        (e.g. {"Name":"Dune","Type":"Sci-Fi","Price":14.99,"PublishDate":"1965"})
        — output ONLY a clean JSON object with these exact four fields:

        {"Name": "<title>", "Type": "<genre>", "Price": <decimal>, "PublishDate": "<date or Unknown>"}

        Rules:
        - If the input is already valid JSON with all four fields, echo it unchanged.
        - Use sensible defaults: Type → "General", Price → 0, PublishDate → "Unknown".
        - Output ONLY the raw JSON object — no markdown fences, no explanation.
        """;

    private const string DescriptionPrompt = """
        You are a professional book editor writing product descriptions for an online bookstore.

        The most recent user message contains JSON metadata about a newly catalogued book.
        Write a compelling 2–3 sentence description that would appear on the book's product page.

        Rules:
        - Be engaging and accurate to the genre.
        - Do NOT start with "This book" or "The book".
        - Output ONLY the prose description — no JSON, no labels, no extra text.
        """;

    private const string InsightPrompt = """
        You are a marketing analyst for a bookstore.

        The conversation contains book metadata (JSON) followed by a product description.
        Based on that information output EXACTLY these three lines and nothing else:

        TARGET_AUDIENCE: <one sentence describing the ideal reader>
        KEY_THEMES: <3–5 comma-separated themes>
        MARKETING_BLURB: <one punchy marketing line, max 12 words>
        """;

    // ── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="Workflow"/> using agent-based sequential composition.
    /// The workflow name <c>"book-enrichment-entry"</c> matches the DevUI entity_id.
    /// </summary>
    public static Workflow Create(IConfiguration config)
    {
        // Each agent gets its own ChatClient so they are independent.
        var extractorAgent   = ChatClientFactory.Create(config).AsAIAgent(ExtractorPrompt,   "BookDataExtractor");
        var descriptionAgent = ChatClientFactory.Create(config).AsAIAgent(DescriptionPrompt, "BookDescriptionWriter");
        var insightAgent     = ChatClientFactory.Create(config).AsAIAgent(InsightPrompt,     "BookInsightAnalyst");

        return AgentWorkflowBuilder.BuildSequential(
            "book-enrichment-entry",
            [extractorAgent, descriptionAgent, insightAgent]);
    }
}
