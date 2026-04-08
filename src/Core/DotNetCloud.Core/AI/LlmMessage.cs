namespace DotNetCloud.Core.AI;

/// <summary>
/// Represents a single message in an LLM conversation.
/// </summary>
/// <param name="Role">The role of the message author: "system", "user", or "assistant".</param>
/// <param name="Content">The text content of the message.</param>
public sealed record LlmMessage(string Role, string Content);
