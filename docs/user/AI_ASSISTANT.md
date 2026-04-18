# AI Assistant — User Guide

> **Last Updated:** 2026-04-09

---

## Welcome

DotNetCloud AI Assistant lets you have multi-turn conversations with a large language model (LLM) directly in your DotNetCloud instance. Use it for brainstorming, writing assistance, code help, summarization, translation, and more — all from your browser.

Your conversations are private. Only you can see your chat history.

---

## Getting Started

1. Open **AI Assistant** from the left sidebar
2. If the AI backend is available, you'll see the welcome screen with "Start a conversation or select one from the sidebar"
3. Click the **+** button in the sidebar header to start a new chat

> **Note:** If you see a warning that "Ollama is not reachable," the AI backend may be down or not configured. Contact your administrator.

---

## Conversations

### Starting a New Conversation

1. Click the **+** button in the sidebar
2. A new conversation called "New Chat" is created
3. Type your message in the text area at the bottom and press **Enter** (or click the send button)
4. The assistant will respond in real time with a streaming response

### Selecting a Model

Use the model dropdown at the top of the sidebar to choose which AI model to use. Different models have different strengths:

- **Smaller models** (7B–8B) respond faster and use fewer resources
- **Larger models** (30B–70B+) produce higher-quality responses but are slower
- Your administrator configures which models are available

The selected model applies to new conversations. Each conversation remembers the model it was created with.

### Switching Between Conversations

Click any conversation in the sidebar to load it. Your full message history is preserved and displayed.

Conversations are sorted with the most recently updated at the top. Each entry shows the conversation title and the date of the last message.

### Renaming a Conversation

1. **Double-click** the conversation title in the sidebar
2. An editable text field appears
3. Type the new name
4. Press **Enter** to save, or click elsewhere to confirm

### Deleting a Conversation

1. Hover over a conversation in the sidebar
2. Click the **✕** button that appears on the right
3. The conversation is removed from your list

> **Note:** Deleted conversations are soft-deleted. An administrator can recover them if needed.

---

## Chatting

### Sending a Message

1. Type your message in the text area at the bottom of the chat
2. Press **Enter** to send (or click the **➤** send button)
3. The assistant's response streams in token by token

While the assistant is responding:
- A blinking cursor **▍** shows the response is being generated
- The send button shows a spinner
- You cannot send another message until the response completes

### Model Loading

If the AI model isn't already loaded in memory (e.g., first request after a server restart), you'll see:

> "Loading model into memory — this may take a moment…"

This is normal. Subsequent messages in the same session will be much faster.

### Message Formatting

The AI assistant renders its responses with full **Markdown** formatting:

- **Bold**, *italic*, ~~strikethrough~~
- Headings, lists, task lists
- Code blocks with syntax highlighting
- Tables, blockquotes, links
- And more

Your messages are displayed as plain text.

---

## Copying Responses

Each assistant response includes two copy buttons:

| Button | What It Copies |
|---|---|
| **📋 Copy as Markdown** | Raw Markdown source — paste into any Markdown editor |
| **📄 Copy as Formatted** | Rich formatted text — paste into documents, emails, etc. |

After copying, the button briefly shows **✓ Copied!** to confirm.

---

## Tips for Better Results

### Be Specific

Instead of: "Tell me about databases"  
Try: "Explain the difference between PostgreSQL and SQL Server for a self-hosted application with 50 users"

### Provide Context

The assistant remembers all messages in the current conversation. You can build on previous answers:

1. "Summarize the key points of the PostgreSQL vs SQL Server comparison"
2. "Now create a pros/cons table based on that"
3. "Which would you recommend for my use case?"

### Use System Prompts (Power Users)

When creating a conversation via the API, you can set a **system prompt** to customize the assistant's behavior:

- "You are a technical writer. Respond in clear, concise language."
- "You are a coding assistant. Always include code examples in Python."
- "Respond in French."

> **Note:** System prompts are not configurable from the web UI at this time. They can be set via the REST API.

### Start Fresh When Needed

If a conversation goes off track, start a new one with the **+** button. Each conversation is independent — the assistant has no memory across different chats.

---

## Privacy

- **Your conversations are private.** Only you can see your conversations and messages. Other users — including administrators — cannot view your chat content through the web interface.
- **Local inference (Ollama):** If your administrator uses Ollama, all AI processing happens on your organization's server. No data is sent to external services.
- **Cloud providers:** If your administrator configured OpenAI or Anthropic, your messages are sent to the respective cloud API. Check with your administrator about the data handling policy.

---

## Troubleshooting

| Issue | What to Do |
|---|---|
| "Ollama is not reachable" warning | The AI backend is down or misconfigured. Contact your administrator. |
| No models in the dropdown | No AI models are installed. Contact your administrator. |
| Slow responses | Large models take longer. Try asking your administrator to switch to a smaller model. |
| Response cuts off mid-sentence | The response may have hit the token limit. Start a follow-up message like "Please continue." |
| "Failed to create conversation" | There may be a temporary server issue. Refresh the page and try again. |
| "Failed to load AI assistant" | The AI module may not be running. Contact your administrator. |
