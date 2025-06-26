using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class ReportGenerationEvent : IEvent
    {
        public string EventId { get; set; } = "";
        public IEventArgs? EventArgs { get; set; }
    }
}
