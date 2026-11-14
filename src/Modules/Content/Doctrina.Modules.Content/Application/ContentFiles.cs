using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doctrina.Modules.Content.Application;

/// <summary>Where content files are kept and how large one may be (<c>Content:Files</c>, design D5 of add-content).</summary>
public sealed class ContentFileOptions
{
    public const string Section = "Content:Files";

    /// <summary>Folder for the files; relative paths are below the content root. Environment variables are expanded.</summary>
    public string Path { get; set; } = "App_Data/content";

    public int MaxSizeMb { get; set; } = 100;

    public long MaxSize => MaxSizeMb * 1024L * 1024L;
}

/// <summary>
/// Content files on disk as <c>{root}/{offeringId}/{itemId}</c> (design D5). The name on disk never comes from user
/// input. To move to object storage later, replace this class.
/// </summary>
internal sealed partial class ContentFileStore(IOptions<ContentFileOptions> options, IHostEnvironment environment, ILogger<ContentFileStore> logger)
{
    private readonly string _root = System.IO.Path.GetFullPath(
        Environment.ExpandEnvironmentVariables(options.Value.Path), environment.ContentRootPath);

    public long MaxSize => options.Value.MaxSize;

    public int MaxSizeMb => options.Value.MaxSizeMb;

    /// <summary>Saves the stream and returns its size, or <see langword="null"/> (and nothing on disk) when it is too large.</summary>
    public async Task<long?> SaveAsync(Guid offeringId, Guid itemId, Stream content, CancellationToken ct)
    {
        var path = PathOf(offeringId, itemId);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        long size = 0;
        try
        {
            await using (var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    size += read;
                    if (size > MaxSize)
                    {
                        break;
                    }

                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }

            if (size > MaxSize)
            {
                return null;
            }

            File.Move(temp, path, overwrite: true);
            return size;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>The file, or <see langword="null"/> when it is not on disk.</summary>
    public Stream? Open(Guid offeringId, Guid itemId)
    {
        var path = PathOf(offeringId, itemId);
        return File.Exists(path)
            // FileShare.Delete: a lecturer can delete a file while a student is still downloading it.
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, useAsync: true)
            : null;
    }

    /// <summary>Removes the file; a failure is logged and leaves an unreachable file behind.</summary>
    public void Delete(Guid offeringId, Guid itemId)
    {
        try
        {
            File.Delete(PathOf(offeringId, itemId));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LogDeleteFailed(logger, itemId, e);
        }
    }

    private string PathOf(Guid offeringId, Guid itemId) => System.IO.Path.Combine(_root, offeringId.ToString("N"), itemId.ToString("N"));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content file of item {ItemId} could not be removed from disk.")]
    private static partial void LogDeleteFailed(ILogger logger, Guid itemId, Exception exception);
}
