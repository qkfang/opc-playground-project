using Proj43.FinOps.Web.Models;

namespace Proj43.FinOps.Web.Services;

/// <summary>
/// Conversational FinOps assistant abstraction. Implemented by the deterministic
/// <see cref="OfflineFinOpsAgent"/> and the live <see cref="Foundry.FoundryFinOpsAgent"/>.
/// Streaming is first-class so the web UI can render tokens over Server-Sent Events.
/// </summary>
public interface IFinOpsAgent
{
    /// <summary>Engine name surfaced in /api/health and the UI badge ("foundry" | "offline").</summary>
    string Name { get; }

    /// <summary>Stream a reply for <paramref name="message"/> within the given conversation.</summary>
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(string conversationId, string message, CancellationToken ct = default);

    /// <summary>Non-streaming convenience: assemble the full reply (used by /api/chat and tests).</summary>
    Task<ChatResponse> ReplyAsync(string conversationId, string message, CancellationToken ct = default);
}
