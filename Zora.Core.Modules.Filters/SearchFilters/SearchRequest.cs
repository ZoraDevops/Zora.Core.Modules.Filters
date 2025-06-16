namespace Zora.Modules.Filters.SearchFilters
{
    public class SearchRequest
    {
        public List<Filter>? Filters { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public bool IncludeDeletedRecords { get; set; } = false;
        public List<Sort>? Sorts { get; set; }
    }
}
