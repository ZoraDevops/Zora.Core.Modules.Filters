using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Zora.Modules.Filters.SearchFilters
{
    public class Sort
    {
        public string PropertyName { get; set; }
        public SortDirection Direction { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SortDirection
    {
        [EnumMember(Value = "ASC")]
        ASC,

        [EnumMember(Value = "DESC")]
        DESC
    }
}
