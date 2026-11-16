namespace Doctrina.UI.Components;

/// <summary>Unieke id's voor koppelingen als <c>aria-labelledby</c> en <c>aria-describedby</c> binnen een pagina.</summary>
internal static class DcIds
{
    private static long _next;

    public static string New(string prefix) => $"dc-{prefix}-{Interlocked.Increment(ref _next)}";
}
