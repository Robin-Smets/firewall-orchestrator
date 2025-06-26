using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class TreeTableColumnChangeVisibilityEventArgs : IEventArgs
    {
        public string ColumnId { get; set; } = "";
        public bool NewVisibility { get; set; }
    }
}