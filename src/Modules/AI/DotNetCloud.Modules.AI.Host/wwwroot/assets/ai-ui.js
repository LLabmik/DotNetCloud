/*
 * AI Assistant module UI.
 *
 * Runs inside a sandboxed frame with an OPAQUE ORIGIN (no allow-same-origin), so:
 *   * no document.cookie / localStorage / sessionStorage — every request goes to this module's own
 *     server through the same proxied path this document was loaded from;
 *   * event.origin is "null" for every message — the parent is identified by the SDK bridge, so never
 *     add your own window.message listener;
 *   * every fetch is cross-origin, so requests use credentials: 'include' and the core proxy answers
 *     the preflight;
 *   * modals are available (allow-modals); external links go through the bridge because a sandboxed
 *     document must not navigate itself.
 *
 * All requests are RELATIVE on purpose (see index.html): the core strips /apps/dotnetcloud.ai before
 * forwarding, so "api/v1/ai" resolves to this process's own REST API.
 *
 * Every string that comes from the server is user data. It is written with textContent or by building
 * DOM nodes; the script never assigns markup strings to the DOM.
 */
(function () {
  "use strict";

  var API = "api/v1/ai";
  var STREAM_SILENCE_LIMIT_MS = 60000;
  var WATCHDOG_INTERVAL_MS = 2000;
  var MD_RENDER_INTERVAL_MS = 120;

  var bridge = window.dncModuleBridge || null;

  /* --------------------------------------------------------------- helpers */

  function ApiError(status, message) {
    this.name = "ApiError";
    this.status = status;
    this.message = message;
  }

  function request(path, options) {
    var init = options || {};
    init.credentials = "include";
    init.headers = init.headers || {};
    init.headers.Accept = "application/json";
    if (init.body !== undefined && typeof init.body !== "string") {
      init.headers["Content-Type"] = "application/json";
      init.body = JSON.stringify(init.body);
    }

    return fetch(API + path, init).then(function (response) {
      if (response.status === 401 || response.status === 403) {
        throw new ApiError(response.status, "Your session has expired. Reload the page and try again.");
      }
      if (response.status === 404) {
        throw new ApiError(404, "Not found.");
      }
      if (!response.ok) {
        throw new ApiError(response.status, "The AI service returned an error (" + response.status + ").");
      }
      if (response.status === 204) {
        return null;
      }
      return response.json().catch(function () {
        return null;
      });
    });
  }

  function el(id) {
    return document.getElementById(id);
  }

  function clear(node) {
    while (node.firstChild) {
      node.removeChild(node.firstChild);
    }
  }

  function show(node, visible) {
    if (visible) {
      node.classList.remove("hidden");
    } else {
      node.classList.add("hidden");
    }
  }

  function formatDate(value) {
    var date = value ? new Date(value) : null;
    if (!date || isNaN(date.getTime())) {
      return "";
    }
    return date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
  }

  function icon(pathData) {
    var ns = "http://www.w3.org/2000/svg";
    var svg = document.createElementNS(ns, "svg");
    svg.setAttribute("viewBox", "0 0 24 24");
    svg.setAttribute("aria-hidden", "true");
    svg.setAttribute("focusable", "false");
    var path = document.createElementNS(ns, "path");
    path.setAttribute("d", pathData);
    svg.appendChild(path);
    return svg;
  }

  var ICONS = {
    delete:
      "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z",
    copy:
      "M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z",
    check: "M9 16.17 4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z",
    assistant:
      "M20 9V7c0-1.1-.9-2-2-2h-3c0-1.66-1.34-3-3-3S9 3.34 9 5H6c-1.1 0-2 .9-2 2v2c-1.66 0-3 1.34-3 3s1.34 3 3 3v4c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2v-2c1.66 0 3-1.34 3-3s-1.34-3-3-3zM7.5 11.5c0-.83.67-1.5 1.5-1.5s1.5.67 1.5 1.5S9.83 13 9 13s-1.5-.67-1.5-1.5zM16 17H8v-2h8v2zm-.5-4c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z",
    person:
      "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z",
  };

  /* ------------------------------------------------------- markdown (safe) */

  var INLINE = /(\*\*[^*\n]+\*\*|`[^`\n]+`|\*[^*\n]+\*|\[[^\]\n]+\]\(https?:\/\/[^\s)]+\))/g;

  function appendInline(parent, text) {
    var index = 0;
    var match;
    INLINE.lastIndex = 0;

    while ((match = INLINE.exec(text)) !== null) {
      if (match.index > index) {
        parent.appendChild(document.createTextNode(text.slice(index, match.index)));
      }

      var token = match[0];
      var node;

      if (token.slice(0, 2) === "**") {
        node = document.createElement("strong");
        node.textContent = token.slice(2, -2);
        parent.appendChild(node);
      } else if (token.charAt(0) === "`") {
        node = document.createElement("code");
        node.textContent = token.slice(1, -1);
        parent.appendChild(node);
      } else if (token.charAt(0) === "*") {
        node = document.createElement("em");
        node.textContent = token.slice(1, -1);
        parent.appendChild(node);
      } else {
        var link = /^\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)$/.exec(token);
        if (link) {
          node = document.createElement("a");
          node.className = "md-link";
          node.setAttribute("href", link[2]);
          node.setAttribute("rel", "noopener noreferrer");
          node.textContent = link[1];
          parent.appendChild(node);
        } else {
          parent.appendChild(document.createTextNode(token));
        }
      }

      index = match.index + token.length;
    }

    if (index < text.length) {
      parent.appendChild(document.createTextNode(text.slice(index)));
    }
  }

  /**
   * Renders the model's markdown into DOM nodes. Raw HTML in the response is never interpreted: every
   * text run becomes a text node, so a model that emits "<img onerror=…>" only produces visible text.
   */
  function renderMarkdown(target, markdown) {
    clear(target);

    var lines = String(markdown || "").replace(/\r\n/g, "\n").split("\n");
    var paragraph = [];
    var index = 0;

    function flushParagraph() {
      if (paragraph.length === 0) {
        return;
      }
      var p = document.createElement("p");
      appendInline(p, paragraph.join(" "));
      target.appendChild(p);
      paragraph = [];
    }

    while (index < lines.length) {
      var line = lines[index];

      var fence = /^```([A-Za-z0-9_+-]*)\s*$/.exec(line);
      if (fence) {
        flushParagraph();
        index++;
        var code = [];
        while (index < lines.length && !/^```\s*$/.test(lines[index])) {
          code.push(lines[index]);
          index++;
        }
        if (index < lines.length) {
          index++;
        }
        var pre = document.createElement("pre");
        var codeNode = document.createElement("code");
        if (fence[1]) {
          codeNode.className = "language-" + fence[1];
        }
        codeNode.textContent = code.join("\n");
        pre.appendChild(codeNode);
        target.appendChild(pre);
        continue;
      }

      if (/^\s*$/.test(line)) {
        flushParagraph();
        index++;
        continue;
      }

      var heading = /^(#{1,6})\s+(.*)$/.exec(line);
      if (heading) {
        flushParagraph();
        var level = Math.min(6, heading[1].length + 2);
        var headingNode = document.createElement("h" + level);
        appendInline(headingNode, heading[2]);
        target.appendChild(headingNode);
        index++;
        continue;
      }

      if (/^\s*[-*]\s+/.test(line)) {
        flushParagraph();
        var ul = document.createElement("ul");
        while (index < lines.length && /^\s*[-*]\s+/.test(lines[index])) {
          var li = document.createElement("li");
          appendInline(li, lines[index].replace(/^\s*[-*]\s+/, ""));
          ul.appendChild(li);
          index++;
        }
        target.appendChild(ul);
        continue;
      }

      if (/^\s*\d+[.)]\s+/.test(line)) {
        flushParagraph();
        var ol = document.createElement("ol");
        while (index < lines.length && /^\s*\d+[.)]\s+/.test(lines[index])) {
          var oli = document.createElement("li");
          appendInline(oli, lines[index].replace(/^\s*\d+[.)]\s+/, ""));
          ol.appendChild(oli);
          index++;
        }
        target.appendChild(ol);
        continue;
      }

      if (/^\s*>\s?/.test(line)) {
        flushParagraph();
        var quote = document.createElement("blockquote");
        while (index < lines.length && /^\s*>\s?/.test(lines[index])) {
          appendInline(quote, lines[index].replace(/^\s*>\s?/, ""));
          if (index + 1 < lines.length && /^\s*>\s?/.test(lines[index + 1])) {
            quote.appendChild(document.createElement("br"));
          }
          index++;
        }
        target.appendChild(quote);
        continue;
      }

      if (/^\s*(-{3,}|\*{3,})\s*$/.test(line)) {
        flushParagraph();
        target.appendChild(document.createElement("hr"));
        index++;
        continue;
      }

      paragraph.push(line.trim());
      index++;
    }

    flushParagraph();
  }

  /* ------------------------------------------------------------- state/dom */

  var state = {
    conversations: [],
    activeId: null,
    active: null,
    defaultModel: "",
    ollamaHealthy: true,
    streaming: false,
    streamAbort: null,
    streamController: null,
    watchdogTimer: null,
    lastChunkAt: 0,
    watchdogArmed: false,
    pendingText: "",
    pendingThinking: "",
    renamingId: null,
    helpOpen: false,
    loading: false,
    copiedKey: null,
    markdownTimer: null,
  };

  var nodes = {
    conversationList: el("conversation-list"),
    conversationEmpty: el("conversation-empty"),
    modelLabel: el("model-label"),
    welcome: el("welcome"),
    chat: el("chat"),
    help: el("help"),
    messages: el("messages"),
    error: el("error"),
    input: el("input"),
    composer: el("composer"),
    send: el("send"),
    ollamaWarning: el("ollama-warning"),
    helpToggle: el("help-toggle"),
    helpClose: el("help-close"),
  };

  /* ---------------------------------------------------------- conversations */

  function setError(message) {
    if (message) {
      nodes.error.textContent = message;
      show(nodes.error, true);
    } else {
      nodes.error.textContent = "";
      show(nodes.error, false);
    }
  }

  function showWelcome(visible) {
    show(nodes.welcome, visible);
    show(nodes.chat, !visible && !state.helpOpen);
    show(nodes.help, state.helpOpen);
  }

  function renderModelLabel() {
    nodes.modelLabel.textContent = "Model: " + (state.defaultModel || "—");
    show(nodes.ollamaWarning, !state.ollamaHealthy && state.active === null);
  }

  function renderConversations() {
    clear(nodes.conversationList);
    show(nodes.conversationEmpty, state.conversations.length === 0);

    state.conversations.forEach(function (conversation) {
      var row = document.createElement("div");
      row.className = "ai-conversation" + (conversation.id === state.activeId ? " is-active" : "");
      row.setAttribute("role", "listitem");

      if (state.renamingId === conversation.id) {
        var input = document.createElement("input");
        input.className = "ai-rename-input";
        input.type = "text";
        input.value = conversation.title || "";
        input.setAttribute("aria-label", "Conversation title");

        var commit = function () {
          renameConversation(conversation, input.value);
        };
        input.addEventListener("blur", commit);
        input.addEventListener("keydown", function (event) {
          if (event.key === "Enter") {
            event.preventDefault();
            input.blur();
          } else if (event.key === "Escape") {
            state.renamingId = null;
            renderConversations();
          }
        });

        row.appendChild(input);
        window.setTimeout(function () {
          input.focus();
          input.select();
        }, 0);
      } else {
        var button = document.createElement("button");
        button.type = "button";
        button.className = "ai-conversation-btn";
        button.title = conversation.title || "Untitled";

        var title = document.createElement("span");
        title.className = "ai-conversation-title";
        title.textContent = conversation.title || "Untitled";
        button.appendChild(title);

        var date = document.createElement("span");
        date.className = "ai-conversation-date";
        date.textContent = formatDate(conversation.updatedAt || conversation.createdAt);
        button.appendChild(date);

        button.addEventListener("click", function () {
          openConversation(conversation.id);
        });
        button.addEventListener("dblclick", function (event) {
          event.preventDefault();
          state.renamingId = conversation.id;
          renderConversations();
        });

        row.appendChild(button);

        var remove = document.createElement("button");
        remove.type = "button";
        remove.className = "ai-icon-btn ai-conversation-delete";
        remove.title = "Delete chat";
        remove.setAttribute("aria-label", "Delete chat");
        remove.appendChild(icon(ICONS.delete));
        remove.addEventListener("click", function (event) {
          event.stopPropagation();
          deleteConversation(conversation.id);
        });
        row.appendChild(remove);
      }

      nodes.conversationList.appendChild(row);
    });
  }

  function addConversationToState(conversation) {
    state.conversations.unshift({
      id: conversation.id,
      title: conversation.title || "New Conversation",
      model: conversation.model,
      createdAt: conversation.createdAt,
      updatedAt: conversation.updatedAt,
    });
  }

  function newChat() {
    if (state.loading) {
      return;
    }

    setError(null);
    state.loading = true;

    request("/conversations", {
      method: "POST",
      body: { title: "New Chat", systemPrompt: null },
    })
      .then(function (conversation) {
        if (!conversation) {
          setError("Failed to create the conversation.");
          return;
        }
        addConversationToState(conversation);
        state.activeId = conversation.id;
        state.active = {
          id: conversation.id,
          title: conversation.title,
          model: conversation.model,
          systemPrompt: conversation.systemPrompt,
          createdAt: conversation.createdAt,
          updatedAt: conversation.updatedAt,
          messages: [],
        };
        renderConversations();
        renderMessages();
        renderModelLabel();
        showWelcome(false);
        nodes.input.focus();
      })
      .catch(function (error) {
        setError(error.message || "Failed to create the conversation.");
      })
      .then(function () {
        state.loading = false;
      });
  }

  function openConversation(id) {
    if (state.streaming) {
      return;
    }

    setError(null);
    state.helpOpen = false;

    request("/conversations/" + encodeURIComponent(id))
      .then(function (detail) {
        if (!detail) {
          setError("Failed to load the conversation.");
          return;
        }
        state.activeId = detail.id;
        state.active = {
          id: detail.id,
          title: detail.title,
          model: detail.model,
          systemPrompt: detail.systemPrompt,
          createdAt: detail.createdAt,
          updatedAt: detail.updatedAt,
          messages: (detail.messages || []).slice(),
        };
        renderConversations();
        renderMessages();
        renderModelLabel();
        showWelcome(false);
        nodes.input.focus();
      })
      .catch(function (error) {
        setError(error.message || "Failed to load the conversation.");
      });
  }

  function renameConversation(conversation, rawTitle) {
    var title = String(rawTitle || "").trim();
    state.renamingId = null;

    if (!title || title === conversation.title) {
      renderConversations();
      return;
    }

    request("/conversations/" + encodeURIComponent(conversation.id) + "/title", {
      method: "PATCH",
      body: { title: title },
    })
      .then(function () {
        conversation.title = title;
        if (state.active && state.active.id === conversation.id) {
          state.active.title = title;
        }
        renderConversations();
      })
      .catch(function (error) {
        setError(error.message || "Failed to rename the conversation.");
        renderConversations();
      });
  }

  function deleteConversation(id) {
    if (state.streaming) {
      return;
    }

    if (!window.confirm("Delete this conversation? This cannot be undone.")) {
      return;
    }

    request("/conversations/" + encodeURIComponent(id), { method: "DELETE" })
      .then(function () {
        state.conversations = state.conversations.filter(function (item) {
          return item.id !== id;
        });
        if (state.activeId === id) {
          state.activeId = null;
          state.active = null;
          renderMessages();
          showWelcome(true);
        }
        renderConversations();
      })
      .catch(function (error) {
        setError(error.message || "Failed to delete the conversation.");
      });
  }

  /* --------------------------------------------------------------- messages */

  function scrollToBottom() {
    var list = nodes.messages;
    if (list) {
      list.scrollTop = list.scrollHeight;
    }
  }

  function renderMessages() {
    clear(nodes.messages);

    if (!state.active) {
      return;
    }

    state.active.messages.forEach(function (message) {
      nodes.messages.appendChild(buildMessage(message));
    });

    if (state.streaming) {
      nodes.messages.appendChild(buildStreamingBubble());
    }

    scrollToBottom();
  }

  function buildMessage(message) {
    var wrapper = document.createElement("div");
    wrapper.className = "ai-msg " + (message.role === "user" ? "is-user" : "is-assistant");

    var avatar = document.createElement("div");
    avatar.className = "ai-avatar";
    avatar.appendChild(icon(message.role === "user" ? ICONS.person : ICONS.assistant));
    wrapper.appendChild(avatar);

    var body = document.createElement("div");
    body.className = "ai-msg-body";

    var role = document.createElement("div");
    role.className = "ai-msg-role";
    role.textContent = message.role === "user" ? "You" : "Assistant";
    body.appendChild(role);

    var text = document.createElement("div");
    text.className = "ai-msg-text";

    if (message.role === "user") {
      // Plain text: the user's own input is never rendered as markup.
      text.textContent = message.content;
    } else {
      text.className += " ai-markdown";
      renderMarkdown(text, message.content);

      var actions = document.createElement("div");
      actions.className = "ai-copy-actions";

      var key = message.id || message.createdAt || message.content;

      var markdownButton = document.createElement("button");
      markdownButton.type = "button";
      markdownButton.className = "ai-copy-btn";
      markdownButton.title = "Copy as Markdown";
      markdownButton.appendChild(icon(state.copiedKey === key + ":md" ? ICONS.check : ICONS.copy));
      var markdownLabel = document.createElement("span");
      markdownLabel.textContent = state.copiedKey === key + ":md" ? "Copied!" : "Copy as Markdown";
      markdownButton.appendChild(markdownLabel);
      markdownButton.addEventListener("click", function () {
        copyMessage(message, text, key, "md");
      });
      actions.appendChild(markdownButton);

      var formattedButton = document.createElement("button");
      formattedButton.type = "button";
      formattedButton.className = "ai-copy-btn";
      formattedButton.title = "Copy as formatted text";
      formattedButton.appendChild(icon(state.copiedKey === key + ":html" ? ICONS.check : ICONS.copy));
      var formattedLabel = document.createElement("span");
      formattedLabel.textContent = state.copiedKey === key + ":html" ? "Copied!" : "Copy as formatted";
      formattedButton.appendChild(formattedLabel);
      formattedButton.addEventListener("click", function () {
        copyMessage(message, text, key, "html");
      });
      actions.appendChild(formattedButton);

      body.appendChild(actions);
    }

    body.appendChild(text);
    wrapper.appendChild(body);
    return wrapper;
  }

  function buildStreamingBubble() {
    var wrapper = document.createElement("div");
    wrapper.className = "ai-msg is-assistant";

    var avatar = document.createElement("div");
    avatar.className = "ai-avatar";
    avatar.appendChild(icon(ICONS.assistant));
    wrapper.appendChild(avatar);

    var body = document.createElement("div");
    body.className = "ai-msg-body";

    var role = document.createElement("div");
    role.className = "ai-msg-role";
    role.textContent = "Assistant";

    var thinkingIndicator = document.createElement("span");
    thinkingIndicator.className = "ai-thinking";
    thinkingIndicator.id = "stream-thinking-indicator";
    thinkingIndicator.textContent = "Thinking…";
    role.appendChild(thinkingIndicator);
    body.appendChild(role);

    var actions = document.createElement("div");
    actions.className = "ai-stream-actions";

    var queue = document.createElement("span");
    queue.className = "ai-queue hidden";
    queue.id = "stream-queue";
    actions.appendChild(queue);

    var abort = document.createElement("button");
    abort.type = "button";
    abort.className = "ai-abort-btn";
    abort.id = "stream-abort";
    abort.textContent = "Abort";
    abort.addEventListener("click", function () {
      abortStream(true);
    });
    actions.appendChild(abort);

    body.appendChild(actions);

    var thinkingBlock = document.createElement("div");
    thinkingBlock.className = "ai-thinking-block hidden";
    thinkingBlock.id = "stream-thinking";
    var thinkingLabel = document.createElement("div");
    thinkingLabel.className = "ai-thinking-label";
    thinkingLabel.textContent = "Thought process";
    thinkingBlock.appendChild(thinkingLabel);
    var thinkingText = document.createElement("div");
    thinkingText.className = "ai-thinking-text";
    thinkingText.id = "stream-thinking-text";
    thinkingBlock.appendChild(thinkingText);
    body.appendChild(thinkingBlock);

    var text = document.createElement("div");
    text.className = "ai-msg-text ai-markdown ai-streaming";
    text.id = "stream-text";
    body.appendChild(text);

    wrapper.appendChild(body);
    return wrapper;
  }

  function updateStreamingBubble() {
    var text = el("stream-text");
    var thinking = el("stream-thinking");
    var thinkingText = el("stream-thinking-text");
    var indicator = el("stream-thinking-indicator");

    if (!text) {
      return;
    }

    if (state.pendingThinking) {
      show(thinking, true);
      thinkingText.textContent = state.pendingThinking;
    } else {
      show(thinking, false);
    }

    if (indicator) {
      show(indicator, !state.pendingText && !state.pendingThinking);
    }

    if (state.markdownTimer === null) {
      state.markdownTimer = window.setTimeout(function () {
        state.markdownTimer = null;
        var current = el("stream-text");
        if (current) {
          renderMarkdown(current, state.pendingText);
          scrollToBottom();
        }
      }, MD_RENDER_INTERVAL_MS);
    }
  }

  /* --------------------------------------------------------------- streaming */

  function setStreaming(streaming) {
    state.streaming = streaming;
    nodes.send.disabled = streaming;
    nodes.input.disabled = streaming;
    nodes.composer.classList.toggle("is-streaming", streaming);
  }

  function startWatchdog() {
    stopWatchdog();
    state.watchdogArmed = false;
    state.lastChunkAt = Date.now();

    state.watchdogTimer = window.setInterval(function () {
      if (!state.streaming) {
        return;
      }
      if (state.watchdogArmed && Date.now() - state.lastChunkAt > STREAM_SILENCE_LIMIT_MS) {
        setError("The assistant stopped responding. Try again.");
        abortStream(false);
      }
    }, WATCHDOG_INTERVAL_MS);
  }

  function stopWatchdog() {
    if (state.watchdogTimer !== null) {
      window.clearInterval(state.watchdogTimer);
      state.watchdogTimer = null;
    }
    if (state.markdownTimer !== null) {
      window.clearTimeout(state.markdownTimer);
      state.markdownTimer = null;
    }
  }

  function abortStream(byUser) {
    if (state.streamController) {
      try {
        state.streamController.abort();
      } catch (e) {
        /* already finished */
      }
      state.streamController = null;
    }

    if (byUser && state.pendingText) {
      // The partial answer is not persisted by the service, so it disappears with the bubble.
      setError(null);
    }

    stopWatchdog();
    state.pendingText = "";
    state.pendingThinking = "";
    setStreaming(false);
    renderMessages();
  }

  function sendMessage(rawText) {
    var message = String(rawText || "").trim();
    if (!message || !state.active || state.streaming) {
      return;
    }

    setError(null);
    state.pendingText = "";
    state.pendingThinking = "";
    state.active.messages.push({
      id: "local-" + Date.now(),
      role: "user",
      content: message,
      createdAt: new Date().toISOString(),
    });

    nodes.input.value = "";
    resizeComposer();
    setStreaming(true);
    renderMessages();
    startWatchdog();

    var controller = new AbortController();
    state.streamController = controller;
    var finished = false;

    fetch(API + "/conversations/" + encodeURIComponent(state.active.id) + "/messages/stream", {
      method: "POST",
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
        Accept: "text/event-stream",
      },
      body: JSON.stringify({ message: message }),
      signal: controller.signal,
    })
      .then(function (response) {
        if (response.status === 401 || response.status === 403) {
          throw new ApiError(response.status, "Your session has expired. Reload the page and try again.");
        }
        if (!response.ok || !response.body) {
          throw new ApiError(response.status, "The assistant is unavailable right now.");
        }

        var reader = response.body.getReader();
        var decoder = new TextDecoder();
        var buffer = "";

        function pump() {
          return reader.read().then(function (result) {
            if (result.done) {
              return;
            }

            buffer += decoder.decode(result.value, { stream: true });

            var boundary;
            while ((boundary = buffer.indexOf("\n\n")) >= 0) {
              var rawEvent = buffer.slice(0, boundary);
              buffer = buffer.slice(boundary + 2);
              handleServerEvent(rawEvent);
            }

            return pump();
          });
        }

        return pump();
      })
      .catch(function (error) {
        if (error && error.name === "AbortError") {
          return;
        }
        setError((error && error.message) || "The assistant is unavailable right now.");
      })
      .then(function () {
        if (finished) {
          return;
        }
        finished = true;
        finishStreaming();
      });
  }

  function handleServerEvent(rawEvent) {
    var dataLine = null;
    rawEvent.split("\n").forEach(function (line) {
      var trimmed = line.trim();
      if (trimmed.indexOf("data:") === 0) {
        dataLine = trimmed.slice(5).trim();
      }
    });

    if (!dataLine || dataLine === "[DONE]") {
      return;
    }

    var payload;
    try {
      payload = JSON.parse(dataLine);
    } catch (e) {
      return;
    }

    if (payload.status === "queued") {
      var queue = el("stream-queue");
      if (queue) {
        queue.textContent = "In queue: position " + (payload.position || 1) + " of " + (payload.total || 1);
        show(queue, true);
      }
      state.lastChunkAt = Date.now();
      return;
    }

    var queuePill = el("stream-queue");
    if (queuePill) {
      show(queuePill, false);
    }

    if (payload.thinking) {
      state.pendingThinking += payload.thinking;
    }

    if (payload.content) {
      state.pendingText += payload.content;
      // The watchdog only arms once real model output arrives: a cold model load can take a minute.
      state.watchdogArmed = true;
    }

    if (payload.content || payload.thinking) {
      state.lastChunkAt = Date.now();
    }

    updateStreamingBubble();
  }

  function finishStreaming() {
    stopWatchdog();
    setStreaming(false);
    state.streamController = null;

    var answer = state.pendingText;
    state.pendingText = "";
    state.pendingThinking = "";

    if (answer && state.active) {
      state.active.messages.push({
        id: "local-" + Date.now(),
        role: "assistant",
        content: answer,
        createdAt: new Date().toISOString(),
      });

      // The service auto-titles a new conversation from its first user message; mirror that here so
      // the sidebar does not keep showing "New Chat" until the next reload.
      var userMessages = state.active.messages.filter(function (item) {
        return item.role === "user";
      });
      if (userMessages.length === 1) {
        var preview = userMessages[0].content;
        state.active.title = preview.length > 60 ? preview.slice(0, 60).trimEnd() + "…" : preview;
        var summary = state.conversations.find(function (item) {
          return item.id === state.active.id;
        });
        if (summary) {
          summary.title = state.active.title;
        }
        renderConversations();
      }
    }

    renderMessages();
  }

  /* ----------------------------------------------------------------- clipboard */

  function copyText(text) {
    if (navigator.clipboard && typeof navigator.clipboard.writeText === "function") {
      return navigator.clipboard.writeText(text).then(
        function () {
          return true;
        },
        function () {
          return legacyCopyText(text);
        },
      );
    }
    return Promise.resolve(legacyCopyText(text));
  }

  function legacyCopyText(text) {
    var area = document.createElement("textarea");
    area.value = text;
    area.setAttribute("readonly", "readonly");
    area.className = "ai-offscreen";
    document.body.appendChild(area);
    area.select();
    var copied = false;
    try {
      copied = document.execCommand("copy");
    } catch (e) {
      copied = false;
    }
    document.body.removeChild(area);
    return copied;
  }

  function copyMessage(message, renderedNode, key, format) {
    var done = function (ok) {
      if (!ok) {
        setError("Copying to the clipboard is not available in this browser.");
        return;
      }
      state.copiedKey = key + ":" + format;
      renderMessages();
      window.setTimeout(function () {
        if (state.copiedKey === key + ":" + format) {
          state.copiedKey = null;
          renderMessages();
        }
      }, 2000);
    };

    if (format === "md") {
      copyText(message.content).then(done);
      return;
    }

    // Rich copy: select the rendered answer and copy it, which keeps headings, lists and code blocks.
    var selection = window.getSelection();
    if (!selection) {
      copyText(message.content).then(done);
      return;
    }

    var range = document.createRange();
    range.selectNodeContents(renderedNode);
    selection.removeAllRanges();
    selection.addRange(range);

    var copied = false;
    try {
      copied = document.execCommand("copy");
    } catch (e) {
      copied = false;
    }
    selection.removeAllRanges();

    if (!copied) {
      copyText(message.content).then(done);
      return;
    }

    done(true);
  }

  /* -------------------------------------------------------------------- init */

  function resizeComposer() {
    nodes.input.style.height = "auto";
    nodes.input.style.height = Math.min(nodes.input.scrollHeight, 220) + "px";
  }

  function openHelp() {
    state.helpOpen = true;
    show(nodes.help, true);
    show(nodes.chat, false);
    show(nodes.welcome, false);
  }

  function closeHelp() {
    state.helpOpen = false;
    show(nodes.help, false);
    show(nodes.chat, state.active !== null);
    show(nodes.welcome, state.active === null);
  }

  function loadInitialState() {
    setError(null);

    var conversations = request("/conversations").catch(function (error) {
      setError(error.message || "Failed to load your conversations.");
      return [];
    });

    var settings = request("/settings").catch(function () {
      return null;
    });

    var health = fetch(API + "/health/ollama", { credentials: "include" })
      .then(function (response) {
        return response.ok;
      })
      .catch(function () {
        return false;
      });

    Promise.all([conversations, settings, health]).then(function (results) {
      state.conversations = (results[0] || []).map(function (conversation) {
        return conversation;
      });
      state.defaultModel = (results[1] && results[1].defaultModel) || "";
      state.ollamaHealthy = results[2] === true;
      renderConversations();
      renderModelLabel();
    });
  }

  document.addEventListener("DOMContentLoaded", function () {
    if (bridge) {
      bridge.start({ title: "AI Assistant" });
      bridge.on("theme", function () {
        /* the bridge sets data-theme on <html>; CSS does the rest */
      });
    }

    el("new-chat").addEventListener("click", newChat);
    nodes.helpToggle.addEventListener("click", openHelp);
    nodes.helpClose.addEventListener("click", closeHelp);

    nodes.composer.addEventListener("submit", function (event) {
      event.preventDefault();
      sendMessage(nodes.input.value);
    });

    nodes.input.addEventListener("keydown", function (event) {
      if (event.key === "Enter" && !event.shiftKey) {
        event.preventDefault();
        sendMessage(nodes.input.value);
      }
    });

    nodes.input.addEventListener("input", resizeComposer);

    // A sandboxed document must not navigate itself, so links open through the core.
    nodes.messages.addEventListener("click", function (event) {
      var link = event.target && event.target.closest ? event.target.closest("a.md-link") : null;
      if (!link) {
        return;
      }
      event.preventDefault();
      var href = link.getAttribute("href");
      if (href && bridge) {
        bridge.openExternal(href);
      }
    });

    resizeComposer();
    showWelcome(true);
    loadInitialState();
  });

  window.addEventListener("beforeunload", function () {
    abortStream(false);
  });
})();
