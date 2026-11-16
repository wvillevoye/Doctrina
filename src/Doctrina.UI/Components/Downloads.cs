using Microsoft.JSInterop;

namespace Doctrina.UI.Components;

/// <summary>
/// Lets the browser save a file the server streams to it, via <c>wwwroot/js/dc-download.js</c> (design D7 of
/// add-bulk-import, shared since design D6 of add-content). For interactive components only.
/// </summary>
public static class Downloads
{
    public const string ModulePath = "./_content/Doctrina.UI/js/dc-download.js";

    public static Task SaveAsync(IJSRuntime js, string fileName, byte[] content) =>
        SaveAsync(js, fileName, new MemoryStream(content));

    /// <summary>Streams <paramref name="content"/> to the browser and disposes it.</summary>
    public static async Task SaveAsync(IJSRuntime js, string fileName, Stream content)
    {
        ArgumentNullException.ThrowIfNull(js);
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        using var stream = new DotNetStreamReference(content);
        await module.InvokeVoidAsync("save", fileName, stream);
    }
}
