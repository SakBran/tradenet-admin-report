using System.Text.Json.Serialization;
using API.Model.ExcelExport;

namespace API.Model
{
    public class ReportQueryRequest
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; } = 10;
        public string? SortColumn { get; set; }
        public string? SortOrder { get; set; }
        public string? FilterColumn { get; set; }
        public string? FilterQuery { get; set; }
        public bool IncludeTotalCount { get; set; }

        /// <summary>
        /// How the grid looked when the user pressed Excel — the title, the visible
        /// columns and where the footer totals go. Sent only on the Excel enqueue call
        /// (the grid's own Post leaves it null, so it is omitted from every other
        /// request payload and from those requests' dedup hash).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ExcelPresentationSpec? Excel { get; set; }
    }
}
