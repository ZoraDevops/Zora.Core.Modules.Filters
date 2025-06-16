namespace Zora.Modules.Filters.SearchFilters
{
    public class Filter
    {
        public string PropertyName { get; set; }
        public object PropertyValue { get; set; }
        public string Comparison { get; set; } // e.g., "Equals", "GreaterThan", "LessThan"
    }
}
