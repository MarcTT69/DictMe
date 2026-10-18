using DictMe.Web.Services;
using Microsoft.AspNetCore.Components;

namespace DictMe.Web.Components;

/// <summary>Composant qui se redessine à chaque changement de <see cref="DictationState"/>.</summary>
public abstract class StateComponent : ComponentBase, IDisposable
{
    [Inject] protected DictationState State { get; set; } = default!;

    protected override void OnInitialized() => State.Changed += HandleStateChanged;

    private void HandleStateChanged()
    {
        OnStateChanged();
        _ = InvokeAsync(StateHasChanged);
    }

    protected virtual void OnStateChanged()
    {
    }

    public virtual void Dispose() => State.Changed -= HandleStateChanged;
}
