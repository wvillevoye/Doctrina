using Microsoft.JSInterop;

namespace Doctrina.Modules.Academic.Components;

/// <summary>Lets the browser save bytes as a file, via <c>wwwroot/js/download.js</c> (design D7 of add-bulk-import).</summary>
internal static class Downloads
{
    private const string ModulePath = "./_content/Doctrina.Modules.Academic/js/download.js";

    public static async Task SaveAsync(IJSRuntime js, string fileName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(js);
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        using var stream = new DotNetStreamReference(new MemoryStream(content));
        await module.InvokeVoidAsync("save", fileName, stream);
    }
}
