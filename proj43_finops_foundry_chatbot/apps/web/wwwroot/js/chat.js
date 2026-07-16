// FinOps Copilot — minimal, no-build chat client.
// Streams answers from /api/chat/stream over Server-Sent Events (parsed from a fetch stream so we can
// POST a body). Renders assistant Markdown→HTML server-side (sent on the 'done' event); during
// streaming it shows raw text with a typing cursor for responsiveness.

(() => {
  const messagesEl = document.getElementById("messages");
  const form = document.getElementById("composer");
  const input = document.getElementById("prompt");
  const sendBtn = document.getElementById("send");
  const engineBadge = document.getElementById("engineBadge");
  const dataBadge = document.getElementById("dataBadge");
  const suggestionsEl = document.getElementById("suggestions");

  let conversationId = null;
  let busy = false;

  // ---- Health + suggestions bootstrap ----
  fetch("/api/health").then(r => r.json()).then(h => {
    engineBadge.textContent = `engine: ${h.engine}`;
    engineBadge.classList.remove("badge-muted");
    engineBadge.classList.add(h.engine === "foundry" ? "badge-live" : "badge-offline");
    if (h.dataThrough) dataBadge.textContent = `data → ${h.dataThrough}`;
    let extra = [];
    if (h.fabricConfigured) extra.push("Fabric");
    if (h.mcpConfigured) extra.push("MCP");
    if (extra.length) dataBadge.title = "Tools: " + extra.join(", ");
  }).catch(() => { engineBadge.textContent = ""; });

  fetch("/api/suggestions").then(r => r.json()).then(list => {
    (list || []).forEach(s => {
      const chip = document.createElement("button");
      chip.type = "button";
      chip.className = "chip";
      chip.textContent = s;
      chip.addEventListener("click", () => { if (!busy) submit(s); });
      suggestionsEl.appendChild(chip);
    });
  }).catch(() => {});

  // ---- Helpers ----
  function clearWelcome() {
    const w = messagesEl.querySelector(".welcome");
    if (w) w.remove();
  }

  function addMessage(role, text) {
    clearWelcome();
    const msg = document.createElement("div");
    msg.className = `msg ${role}`;
    const avatar = document.createElement("div");
    avatar.className = "avatar";
    avatar.textContent = role === "user" ? "🧑" : "🤖";
    const bubble = document.createElement("div");
    bubble.className = "bubble";
    bubble.textContent = text || "";
    msg.appendChild(avatar);
    msg.appendChild(bubble);
    messagesEl.appendChild(msg);
    scroll();
    return bubble;
  }

  function addStatus(text) {
    const s = document.createElement("div");
    s.className = "status-line";
    s.textContent = text;
    messagesEl.appendChild(s);
    scroll();
    return s;
  }

  function scroll() { messagesEl.scrollTop = messagesEl.scrollHeight; }

  function escapeHtml(s) {
    return (s || "").replace(/[&<>"']/g, c =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  }

  // ---- Core submit + SSE parse ----
  async function submit(text) {
    if (busy) return;
    text = (text || input.value || "").trim();
    if (!text) return;
    busy = true;
    sendBtn.disabled = true;
    input.value = "";

    addMessage("user", text);
    let statusEl = addStatus("…");
    const bubble = addMessage("assistant", "");
    bubble.classList.add("cursor");
    let acc = "";

    try {
      const res = await fetch("/api/chat/stream", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ conversationId, message: text }),
      });
      if (!res.ok || !res.body) throw new Error("stream failed: " + res.status);

      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });

        // SSE frames separated by a blank line.
        let idx;
        while ((idx = buffer.indexOf("\n\n")) >= 0) {
          const frame = buffer.slice(0, idx);
          buffer = buffer.slice(idx + 2);
          const dataLine = frame.split("\n").find(l => l.startsWith("data:"));
          if (!dataLine) continue;
          let ev;
          try { ev = JSON.parse(dataLine.slice(5).trim()); } catch { continue; }
          handleEvent(ev);
        }
      }

      function handleEvent(ev) {
        switch (ev.type) {
          case "meta":
            if (ev.conversationId) conversationId = ev.conversationId;
            if (ev.engine) {
              engineBadge.textContent = `engine: ${ev.engine}`;
              engineBadge.classList.remove("badge-muted", "badge-live", "badge-offline");
              engineBadge.classList.add(ev.engine === "foundry" ? "badge-live" : "badge-offline");
            }
            break;
          case "status":
            if (statusEl) statusEl.textContent = ev.data || "";
            break;
          case "token":
            acc += ev.data || "";
            bubble.textContent = acc;
            scroll();
            break;
          case "done":
            if (ev.conversationId) conversationId = ev.conversationId;
            if (ev.data) bubble.innerHTML = ev.data;     // server-rendered safe HTML
            else bubble.textContent = acc;
            bubble.classList.remove("cursor");
            if (statusEl) { statusEl.remove(); statusEl = null; }
            break;
          case "error":
            bubble.classList.remove("cursor");
            bubble.innerHTML = `<em>Error: ${escapeHtml(ev.data || "unknown")}</em>`;
            if (statusEl) { statusEl.remove(); statusEl = null; }
            break;
        }
      }
    } catch (err) {
      bubble.classList.remove("cursor");
      bubble.innerHTML = `<em>Could not reach the assistant (${escapeHtml(String(err.message || err))}).</em>`;
      if (statusEl) { statusEl.remove(); }
    } finally {
      busy = false;
      sendBtn.disabled = false;
      input.focus();
    }
  }

  form.addEventListener("submit", (e) => { e.preventDefault(); submit(); });
  input.focus();
})();
