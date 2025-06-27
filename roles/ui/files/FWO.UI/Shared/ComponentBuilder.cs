using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FWO.Ui.Shared
{
    public class ComponentBuilder<TComponent> where TComponent : IComponent
    {
        private readonly RenderTreeBuilder builder;
        private int seq = 0;

        public ComponentBuilder(RenderTreeBuilder builder)
        {
            this.builder = builder;
        }

        public void Add(string name, object? value)
        {
            builder.AddAttribute(seq++, name, value);
        }

        public void AddContent(string? text)
        {
            builder.AddContent(seq++, text);
        }

        public void OpenElement(string elementName)
        {
            builder.OpenElement(seq++, elementName);
        }

        public void CloseElement()
        {
            builder.CloseElement();
        }

        public void OpenComponent()
        {
            builder.OpenComponent<TComponent>(seq++);
        }

        public void CloseComponent()
        {
            builder.CloseComponent();
        }
        
        public ComponentBuilder<TComponent> AddFluent(string name, object? value)
        {
            builder.AddAttribute(seq++, name, value);
            return this;
        }

        public ComponentBuilder<TComponent> AddContentFluent(string? text)
        {
            builder.AddContent(seq++, text);
            return this;
        }

    }
    
}