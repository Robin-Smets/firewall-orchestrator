using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class CollectionChangedEvent(CollectionChangedEventArgs? eventArgs = default) : IEvent
    {
        public string EventId { get; set; } = "";
        IEventArgs? IEvent.EventArgs { get; set; }
    }
}
