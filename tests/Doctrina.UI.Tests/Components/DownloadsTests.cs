using Bunit;
using Doctrina.UI.Components;
using Microsoft.JSInterop;

namespace Doctrina.UI.Tests.Components;

// The shared download helper (change add-content, task 1.1, design D6).
public sealed class BestandenWordenViaHetGedeeldeScriptOpgeslagen : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public async Task DownloadRoeptHetScriptAanMetBestandsnaamEnStream()
    {
        var module = _context.JSInterop.SetupModule(Downloads.ModulePath);
        module.SetupVoid("save", _ => true).SetVoidResult();

        await Downloads.SaveAsync(_context.JSInterop.JSRuntime, "syllabus.pdf", new MemoryStream([1, 2, 3]));

        var save = Assert.Single(module.Invocations["save"]);
        Assert.Equal("syllabus.pdf", save.Arguments[0]);
        Assert.IsType<DotNetStreamReference>(save.Arguments[1]);
    }

    public void Dispose() => _context.Dispose();
}
