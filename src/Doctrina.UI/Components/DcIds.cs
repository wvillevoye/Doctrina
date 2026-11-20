namespace Doctrina.UI.Components;

/// <summary>Unique ids for links such as <c>aria-labelledby</c> and <c>aria-describedby</c> within a page.</summary>
internal static class DcIds
{
    private static long _next;

    public static string New(string prefix) => $"dc-{prefix}-{Interlocked.Increment(ref _next)}";
}
