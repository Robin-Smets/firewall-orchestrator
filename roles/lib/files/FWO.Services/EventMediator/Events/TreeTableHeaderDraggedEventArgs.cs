using FWO.Services.EventMediator.Interfaces;

namespace FWO.Services.EventMediator.Events
{
    public class TreeTableHeaderDraggedEventArgs : IEventArgs
    {
        public string DraggedColumnId { get; set; } = "";
        public string TargetColumnId { get; set; } = "";
    }
    
}