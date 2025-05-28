
namespace FWO.Services
{
    public class ReportRulesStructureService : IReportRulesStructureService
    {
        public List<int> CurrentRulePath { get; set; }

        public ReportRulesStructureService()
        {
            CurrentRulePath = [];
        }
        
    }
}
