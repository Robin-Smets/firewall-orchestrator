using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Report
{
    public class ComplianceFilter
    {
        [JsonProperty("is_diff_report"), JsonPropertyName("is_diff_report")]
        public bool IsDiffReport { get; set; } = false;
        [JsonProperty("diff_reference_in_days"), JsonPropertyName("diff_reference_in_days")]
        public int DiffReferenceInDays { get; set; } = 0;
        [JsonProperty("show_compliant_rules"), JsonPropertyName("show_compliant_rules")]
        public bool ShowCompliantRules { get; set; } = false;
        [JsonProperty("show_violations_of_initial_import"), JsonPropertyName("show_violations_of_initial_import")]
        public bool ShowViolationsOfInitialImport { get; set; } = false;

        public ComplianceFilter()
        {

        }

        public ComplianceFilter(ComplianceFilter complianceFilter)
        {
            IsDiffReport = complianceFilter.IsDiffReport;
            DiffReferenceInDays = complianceFilter.DiffReferenceInDays;
            ShowCompliantRules = complianceFilter.ShowCompliantRules;
            ShowViolationsOfInitialImport = complianceFilter.ShowViolationsOfInitialImport;
        }
    }

}