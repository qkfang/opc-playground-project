using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Proj43.FinOps.Web.Models;
using Xunit;

namespace Proj43.FinOps.Tests;

/// <summary>End-to-end API checks via WebApplicationFactory (offline engine, no Azure needed).</summary>
public sealed class ChatApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ChatApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_Returns_Ok_And_Offline_Engine()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("ok", doc.GetProperty("status").GetString());
        Assert.Equal("offline", doc.GetProperty("engine").GetString());
        Assert.False(doc.GetProperty("foundryConfigured").GetBoolean());
    }

    [Fact]
    public async Task Suggestions_Returns_NonEmpty_List()
    {
        var client = _factory.CreateClient();
        var list = await client.GetFromJsonAsync<string[]>("/api/suggestions");
        Assert.NotNull(list);
        Assert.NotEmpty(list!);
    }

    [Fact]
    public async Task Chat_Returns_Grounded_Reply_With_Html()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/chat", new ChatRequest { Message = "top 5 services by cost" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Equal("offline", body!.Engine);
        Assert.Contains("USD", body.Reply);
        Assert.Contains("<table", body.ReplyHtml);
        Assert.False(string.IsNullOrWhiteSpace(body.ConversationId));
    }

    [Fact]
    public async Task Chat_Empty_Message_Is_BadRequest()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/chat", new ChatRequest { Message = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Chat_Maintains_Conversation_Across_Turns()
    {
        var client = _factory.CreateClient();
        var first = await (await client.PostAsJsonAsync("/api/chat", new ChatRequest { Message = "what did we spend last month" }))
            .Content.ReadFromJsonAsync<ChatResponse>();
        var cid = first!.ConversationId;
        var second = await (await client.PostAsJsonAsync("/api/chat", new ChatRequest { ConversationId = cid, Message = "and the trend?" }))
            .Content.ReadFromJsonAsync<ChatResponse>();
        Assert.Equal(cid, second!.ConversationId);
    }

    [Fact]
    public async Task ChatStream_Emits_Sse_Frames_With_Done()
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
        {
            Content = new StringContent(JsonSerializer.Serialize(new ChatRequest { Message = "any anomalies?" }),
                Encoding.UTF8, "application/json"),
        };
        var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        res.EnsureSuccessStatusCode();
        Assert.StartsWith("text/event-stream", res.Content.Headers.ContentType!.MediaType!);
        var text = await res.Content.ReadAsStringAsync();
        Assert.Contains("event: meta", text);
        Assert.Contains("event: token", text);
        Assert.Contains("event: done", text);
    }
}
