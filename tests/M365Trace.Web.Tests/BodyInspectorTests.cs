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
    public void BomPrefixedJson_EnablesFormattedJsonView()
    {
        var content = new TraceContent(
            "\uFEFF{\"status\":\"ok\"}",
            "application/json",
            18,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        var jsonButton = component.FindAll("button")
            .Single(button => button.TextContent == "JSON");
        Assert.False(jsonButton.HasAttribute("disabled"));

        jsonButton.Click();

        Assert.Contains("\"status\": \"ok\"", component.Find("pre").TextContent);
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
    public void JsonCommentsAndTrailingCommas_AreFormatted()
    {
        var content = new TraceContent(
            """{/* diagnostic */"status":"ok",}""",
            "application/json",
            34,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        component.FindAll("button")
            .Single(button => button.TextContent == "JSON")
            .Click();

        Assert.Contains("\"status\": \"ok\"", component.Find("pre").TextContent);
    }

    [Fact]
    public void StandardXml_IsFormatted()
    {
        var content = new TraceContent(
            "<root><value>diagnostic</value></root>",
            "application/xml",
            38,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        component.FindAll("button")
            .Single(button => button.TextContent == "XML")
            .Click();

        var formattedXml = component.Find("pre").TextContent;
        Assert.Contains("\n", formattedXml, StringComparison.Ordinal);
        Assert.Contains("<value>diagnostic</value>", formattedXml);
    }

    [Fact]
    public void XmlDocumentType_IsRejected()
    {
        var content = new TraceContent(
            """<!DOCTYPE root [<!ENTITY xxe SYSTEM "file:///secret">]><root>&xxe;</root>""",
            "application/xml",
            73,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        var xmlButton = component.FindAll("button")
            .Single(button => button.TextContent == "XML");

        Assert.True(xmlButton.HasAttribute("disabled"));
    }

    [Fact]
    public void ExchangeXmlWithInvalidCharacterReference_EnablesFormattedXmlView()
    {
        var content = new TraceContent(
            """<?xml version="1.0" encoding="utf-8"?><s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" xmlns:t="http://schemas.microsoft.com/exchange/services/2006/types"><s:Body><t:Value>&#xFFFE;</t:Value></s:Body></s:Envelope>""",
            "text/xml",
            250,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        var xmlButton = component.FindAll("button")
            .Single(button => button.TextContent == "XML");
        Assert.False(xmlButton.HasAttribute("disabled"));

        xmlButton.Click();

        var formattedXml = component.Find("pre").TextContent;
        Assert.Contains(
            "<t:Value>&amp;#xFFFE;</t:Value>",
            formattedXml,
            StringComparison.Ordinal);
        Assert.Contains(
            "\n",
            formattedXml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedXml_DisablesFormattedXmlView()
    {
        var content = new TraceContent(
            "<root><unclosed></root>",
            "text/xml",
            23,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        var xmlButton = component.FindAll("button")
            .Single(button => button.TextContent == "XML");

        Assert.True(xmlButton.HasAttribute("disabled"));
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
    public void NonHtmlContent_DisablesHtmlPreview()
    {
        var content = new TraceContent(
            "plain diagnostic text",
            "text/plain",
            21,
            false,
            false);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        var htmlButton = component.FindAll("button")
            .Single(button => button.TextContent == "HTML");

        Assert.True(htmlButton.HasAttribute("disabled"));
    }

    [Fact]
    public void SupportedImage_UsesDataUriPreview()
    {
        var content = new TraceContent(
            null,
            "image/png",
            4,
            true,
            false,
            "iVBORw==");

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        component.FindAll("button")
            .Single(button => button.TextContent == "Image")
            .Click();

        Assert.Equal(
            "data:image/png;base64,iVBORw==",
            component.Find("img").GetAttribute("src"));
    }

    [Fact]
    public void UnsupportedBinaryContent_ExplainsWhyItCannotBeDisplayed()
    {
        var content = new TraceContent(
            null,
            "application/octet-stream",
            4,
            true,
            false,
            null);

        var component = _context.Render<BodyInspector>(
            parameters => parameters.Add(item => item.Content, content));

        Assert.Contains(
            "Binary Base64 content is not displayed.",
            component.Find("pre").TextContent);
        Assert.True(
            component.FindAll("button")
                .Single(button => button.TextContent == "Image")
                .HasAttribute("disabled"));
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
