using Markdig;

namespace Proj43.FinOps.Web.Services;

/// <summary>
/// Renders assistant Markdown (including pipe tables emitted for Fabric/FinOps data) to safe HTML for
/// the chat bubble. Uses Markdig with advanced extensions (tables, autolinks) and disables raw HTML so
/// model/tool output can't inject markup.
/// </summary>
public sealed class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public string ToHtml(string markdown) =>
        string.IsNullOrWhiteSpace(markdown) ? string.Empty : Markdown.ToHtml(markdown, _pipeline);
}
