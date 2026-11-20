using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Doctrina.UI.Components;

/// <summary>
/// Common base of the form fields (spec ui-components "Formuliervelden zijn gekoppeld aan label en
/// foutmelding"): visible label linked via <c>for</c>/<c>id</c>, required marker, and on errors
/// <c>aria-invalid</c> plus <c>aria-describedby</c> pointing to the error message. Works inside <see cref="DcForm"/> or an
/// <see cref="EditForm"/>; use with <c>@bind-Value</c>.
/// </summary>
/// <typeparam name="TValue">Type of the value.</typeparam>
public abstract class DcFieldBase<TValue> : ComponentBase, IDisposable
{
    private EditContext? _subscribedContext;

    /// <summary>Visible label.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public TValue? Value { get; set; }

    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Set by <c>@bind-Value</c>; determines the field name for validation.</summary>
    [Parameter]
    public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>Required field: marked on the input element and in the label.</summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>Short explanation below the field, linked via <c>aria-describedby</c>.</summary>
    [Parameter]
    public string? Hint { get; set; }

    /// <summary>Own id of the input element; by default a unique id.</summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>Extra attributes for the input element, e.g. <c>autocomplete</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [CascadingParameter]
    protected EditContext? EditContext { get; set; }

    /// <summary>Id of the input element.</summary>
    protected string InputId { get; private set; } = string.Empty;

    /// <summary>Id of the error message (<see cref="DcFieldError"/>).</summary>
    protected string ErrorId => InputId + "-fout";

    /// <summary>Id of the explanation.</summary>
    protected string HintId => InputId + "-hint";

    /// <summary>The field in the <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/>.</summary>
    protected FieldIdentifier Field { get; private set; }

    /// <summary>Does the field have validation errors?</summary>
    protected bool IsInvalid => EditContext is not null && ValueExpression is not null && EditContext.GetValidationMessages(Field).Any();

    /// <summary>Attributes for the input element: required, description, custom attributes.</summary>
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
