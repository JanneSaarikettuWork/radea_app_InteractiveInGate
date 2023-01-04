// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//
//    using InteractiveInGate.Models.Json;
//
//    var config = Config.FromJson(jsonString);

namespace InteractiveInGate.Models.Json
{
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class Config
    {
        [JsonProperty("Localization", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public string Localization { get; set; }

        [JsonProperty("AutoConfirmTimeout", Required = Required.Always)]
        public long AutoConfirmTimeout { get; set; }

        [JsonProperty("TagsWaitTimeout", Required = Required.Always)]
        public long TagsWaitTimeout { get; set; }

        [JsonProperty("SortLocations", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public bool SortLocations { get; set; }

        [JsonProperty("Report", Required = Required.Always)]
        public Report Report { get; set; }

        [JsonProperty("executor", Required = Required.Always)]
        public Process.Json.Config Executor { get; set; }

        [JsonProperty("StreamInventory", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public bool StreamInventory { get; set; }

        [JsonProperty("ColorScheme", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public ColorScheme ColorScheme { get; set; }
    }

    public partial class Report
    {
        [JsonProperty("grouping")]
        public List<string> Grouping { get; set; }

        [JsonProperty("reporting")]
        public List<string> Reporting { get; set; }

    }

    public partial class ColorScheme
    {
        [JsonProperty("WeekDay", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, ColorSchemeElement> WeekDay { get; set; }
    }

    public partial class ColorSchemeElement
    {
        [JsonProperty("BackgroundColor", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public string BackgroundColor { get; set; }
    }

    public partial class Config
    {
        public static Config FromJson(string json) => JsonConvert.DeserializeObject<Config>(json, Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this Config self) => JsonConvert.SerializeObject(self, Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

}
