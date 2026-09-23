using Bunit;
using M365Trace.Core;
using M365Trace.Web.Components;

namespace M365Trace.Web.Tests;

public sealed class BodyInspectorTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void ValidJson_EnablesFormattedJsonView()
    {
        var content = new TraceContent(
            """{"status":"ok","count":2}""",
            "application/json",
            25,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        var jsonButton = component.FindAll("button")
            .Single(button => button.TextContent == "JSON");
        Assert.False(jsonButton.HasAttribute("disabled"));

        jsonButton.Click();

        Assert.Contains("\"status\": \"ok\"", component.Find("pre").TextContent);
        Assert.Contains("\"count\": 2", component.Find("pre").TextContent);
    }

    [Fact]
    public void InvalidJson_DisablesFormattedJsonView()
    {
        var content = new TraceContent(
            "{not-json}",
            "application/json",
            10,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        var jsonButton = component.FindAll("button")
            .Single(button => button.TextContent == "JSON");

        Assert.True(jsonButton.HasAttribute("disabled"));
    }

    [Fact]
    public void HtmlPreview_IsSandboxedAndBlocksExternalResources()
    {
        var content = new TraceContent(
            "<html><body>diagnostic</body></html>",
            "text/html",
            36,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        component.FindAll("button")
            .Single(button => button.TextContent == "HTML")
            .Click();

        var frame = component.Find("iframe");
        Assert.True(frame.HasAttribute("sandbox"));
        Assert.Equal("no-referrer", frame.GetAttribute("referrerpolicy"));
        Assert.Contains(
            "Content-Security-Policy",
            frame.GetAttribute("srcdoc"),
            StringComparison.Ordinal);
        Assert.Contains(
            "default-src 'none'",
            frame.GetAttribute("srcdoc"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatedContent_IsClearlyIdentified()
    {
        var content = new TraceContent(
            "partial",
            "text/plain",
            100,
            false,
            true);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        Assert.Contains("Display truncated", component.Markup);
    }

    public void Dispose() => _context.Dispose();
}
