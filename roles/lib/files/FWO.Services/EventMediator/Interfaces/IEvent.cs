namespace FWO.Services.EventMediator.Interfaces
{
    public interface IEvent
    {
        public string EventId { get; set; }
        public IEventArgs? EventArgs { get; set; }
    }
}

