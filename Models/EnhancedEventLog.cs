using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AnalyzerCore.Models
{
    public partial class EnhancedEventLog
    {
        [JsonProperty("address", NullValueHandling = NullValueHandling.Ignore)]
        public string Address { get; set; }

        [JsonProperty("topics", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Topics { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public string Data { get; set; }

        [JsonProperty("blockNumber", NullValueHandling = NullValueHandling.Ignore)]
        public string BlockNumber { get; set; }

        [JsonProperty("transactionHash", NullValueHandling = NullValueHandling.Ignore)]
        public string TransactionHash { get; set; }

        [JsonProperty("transactionIndex", NullValueHandling = NullValueHandling.Ignore)]
        public string TransactionIndex { get; set; }

        [JsonProperty("blockHash", NullValueHandling = NullValueHandling.Ignore)]
        public string BlockHash { get; set; }

        [JsonProperty("logIndex", NullValueHandling = NullValueHandling.Ignore)]
        public string LogIndex { get; set; }

        [JsonProperty("removed", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Removed { get; set; }
    }

    public partial class EnhancedEventLog
    {
        public static List<EnhancedEventLog> FromJson(string json) =>
            JsonConvert.DeserializeObject<List<EnhancedEventLog>>(json, Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this List<EnhancedEventLog> self) =>
            JsonConvert.SerializeObject(self, Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter {DateTimeStyles = DateTimeStyles.AssumeUniversal}
            },
        };
    }
}