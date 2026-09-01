// Co-located JS module for AiChatPage.razor (RCL — served at
// _content/DotNetCloud.Modules.AI/AiChatPage.razor.js).
// Keeps the chat pinned to the newest generated token while streaming.

/// <summary>
/// Scrolls the streaming output region and the message list to the bottom so
/// the latest generated text is always visible without manual scrolling.
/// </summary>
/// <param name="messagesElement">The scrollable message list element (.ai-messages).</param>
/// <param name="streamElement">The internally scrollable streaming region (.ai-stream-scroll), if present.</param>
export function scrollChatToBottom(messagesElement, streamElement) {
    if (streamElement) {
        streamElement.scrollTop = streamElement.scrollHeight;
    }
    if (messagesElement) {
        messagesElement.scrollTop = messagesElement.scrollHeight;
    }
}
