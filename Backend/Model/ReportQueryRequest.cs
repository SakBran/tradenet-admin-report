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
    }
}
