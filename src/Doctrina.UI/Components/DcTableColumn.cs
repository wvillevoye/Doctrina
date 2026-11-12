namespace Doctrina.UI.Components;

/// <summary>Kolom van een <see cref="DcTable{TItem}"/>: kop plus de waarde of een eigen weergave per rij.</summary>
/// <typeparam name="TItem">Type van de rijen.</typeparam>
/// <param name="Title">Kolomkop (wordt een <c>th scope="col"</c>).</param>
/// <param name="Value">Tekstwaarde van een rij; genegeerd als <paramref name="Template"/> is gezet.</param>
/// <param name="Template">Eigen weergave van een cel, bv. een link of badge.</param>
public sealed record DcTableColumn<TItem>(
    string Title,
    Func<TItem, object?>? Value = null,
    Microsoft.AspNetCore.Components.RenderFragment<TItem>? Template = null);
