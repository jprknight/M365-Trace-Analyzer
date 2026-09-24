using Microsoft.JSInterop;

namespace M365Trace.Web.Services;

public interface IClipboardService
{
    ValueTask<ClipboardWriteResult> WriteTextAsync(string text);
}

public sealed class ClipboardService(IJSRuntime jsRuntime) : IClipboardService
{
    public async ValueTask<ClipboardWriteResult> WriteTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            await jsRuntime.InvokeVoidAsync(
                "m365Clipboard.writeText",
                text);
            return ClipboardWriteResult.Success;
        }
        catch (JSException exception)
        {
            return new ClipboardWriteResult(
                false,
                exception.Message);
        }
    }
}

public sealed record ClipboardWriteResult(
    bool Succeeded,
    string? ErrorMessage)
{
    public static ClipboardWriteResult Success { get; } =
        new(true, null);
}
