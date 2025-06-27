using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;

public static class ComponentFactory
{
    public static RenderFragment Build<TComponent>(Action<ComponentBuilder<TComponent>> config) where TComponent : IComponent
    {
        return builder =>
        {
            var cb = new ComponentBuilder<TComponent>(builder);
            cb.OpenComponent();
            config(cb);
            cb.CloseComponent();
        };
    }
}
