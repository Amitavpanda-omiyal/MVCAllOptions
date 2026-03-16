using Volo.Abp.AI;

namespace MVCAllOptions;

/// <summary>
/// Workspace marker for the OpenAIRAGWorkspace configured in AI Management.
/// Used to inject IChatClient&lt;OpenAIRAGWorkspace&gt; backed by the "OpenAIRAGWorkspace" workspace.
/// </summary>
[WorkspaceName("OpenAIRAGWorkspace")]
public class OpenAIRAGWorkspace
{
}
