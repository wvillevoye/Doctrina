using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Doctrina.UI.Components;

/// <summary>
/// Gemeenschappelijke basis van de formuliervelden (spec ui-components "Formuliervelden zijn gekoppeld aan label en
/// foutmelding"): zichtbaar label gekoppeld via <c>for</c>/<c>id</c>, verplicht-markering, en bij fouten
/// <c>aria-invalid</c> plus <c>aria-describedby</c> naar de foutmelding. Werkt binnen <see cref="DcForm"/> of een
/// <see cref="EditForm"/>; gebruik met <c>@bind-Value</c>.
/// </summary>
/// <typeparam name="TValue">Type van de waarde.</typeparam>
public abstract class DcFieldBase<TValue> : ComponentBase, IDisposable
{
    private EditContext? _subscribedContext;

    /// <summary>Zichtbaar label.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public TValue? Value { get; set; }

    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Gezet door <c>@bind-Value</c>; bepaalt de veldnaam voor validatie.</summary>
    [Parameter]
    public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>Verplicht veld: gemarkeerd op het invoerelement en in het label.</summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>Korte toelichting onder het veld, gekoppeld via <c>aria-describedby</c>.</summary>
    [Parameter]
    public string? Hint { get; set; }

    /// <summary>Eigen id van het invoerelement; standaard een unieke id.</summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>Extra attributen voor het invoerelement, bv. <c>autocomplete</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [CascadingParameter]
    protected EditContext? EditContext { get; set; }

    /// <summary>Id van het invoerelement.</summary>
    protected string InputId { get; private set; } = string.Empty;

    /// <summary>Id van de foutmelding (<see cref="DcFieldError"/>).</summary>
    protected string ErrorId => InputId + "-fout";

    /// <summary>Id van de toelichting.</summary>
    protected string HintId => InputId + "-hint";

    /// <summary>Het veld in het <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/>.</summary>
    protected FieldIdentifier Field { get; private set; }

    /// <summary>Heeft het veld validatiefouten?</summary>
    protected bool IsInvalid => EditContext is not null && ValueExpression is not null && EditContext.GetValidationMessages(Field).Any();

    /// <summary>Attributen voor het invoerelement: verplicht, beschrijving, eigen attributen.</summary>
    protected IReadOnlyDictionary<string, object> InputAttributes
    {
        get
        {
            var attributes = AdditionalAttributes is null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(AdditionalAttributes, StringComparer.Ordinal);
            var describedBy = string.Join(' ', new[] { Hint is null ? null : HintId, IsInvalid ? ErrorId : null }.OfType<string>());
            if (describedBy.Length > 0)
            {
                attributes["aria-describedby"] = describedBy;
            }

            if (IsInvalid)
            {
                attributes["aria-invalid"] = "true";
            }

            if (Required)
            {
                attributes["required"] = true;
                attributes["aria-required"] = "true";
            }

            return attributes;
        }
    }

    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new InvalidOperationException($"{GetType().Name} heeft een zichtbaar label nodig.");
        }

        if (ValueExpression is null)
        {
            throw new InvalidOperationException($"{GetType().Name} heeft @bind-Value (of ValueExpression) nodig.");
        }

        InputId = Id ?? (InputId.Length > 0 ? InputId : DcIds.New("veld"));
        Field = FieldIdentifier.Create(ValueExpression);

        if (!ReferenceEquals(EditContext, _subscribedContext))
        {
            Unsubscribe();
            _subscribedContext = EditContext;
            if (_subscribedContext is not null)
            {
                _subscribedContext.OnValidationStateChanged += HandleValidationStateChanged;
            }
        }
    }

    public void Dispose()
    {
        Unsubscribe();
        GC.SuppressFinalize(this);
    }

    private void HandleValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) => StateHasChanged();

    private void Unsubscribe()
    {
        if (_subscribedContext is not null)
        {
            _subscribedContext.OnValidationStateChanged -= HandleValidationStateChanged;
        }
    }
}
