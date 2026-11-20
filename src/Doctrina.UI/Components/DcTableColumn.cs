namespace Doctrina.UI.Components;

/// <summary>Column of a <see cref="DcTable{TItem}"/>: heading plus the value or a custom rendering per row.</summary>
/// <typeparam name="TItem">Type of the rows.</typeparam>
/// <param name="Title">Column heading (becomes a <c>th scope="col"</c>).</param>
/// <param name="Value">Text value of a row; ignored if <paramref name="Template"/> is set.</param>
/// <param name="Template">Custom rendering of a cell, e.g. a link or badge.</param>
public sealed record DcTableColumn<TItem>(
    string Title,
    Func<TItem, object?>? Value = null,
    Microsoft.AspNetCore.Components.RenderFragment<TItem>? Template = null);
