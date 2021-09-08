using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace AnalyzerCore.Models
{
    public class ConfigurationModels
    {
    }

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class ServicesConfig
    {
        [JsonProperty("DataCollector")] public bool DataCollector { get; set; }

        [JsonProperty("AnalyzerService")] public bool AnalyzerService { get; set; }

        [JsonProperty("TokenAnalyzer")] public bool TokenAnalyzer { get; set; }
        
        [JsonProperty("NewTokenService")] public bool NewTokenService { get; set; }

        [JsonProperty("MaxParallelism")] public int MaxParallelism { get; set; }
    }

    public class Heco
    {
        [JsonProperty("RpcEndpoint")] public string RpcEndpoint { get; set; }
        [JsonProperty("Enemies")] public List<string> Enemies { get; set; }

        [JsonProperty("Address")] public string Address { get; set; }

        [JsonProperty("ServicesConfig")] public ServicesConfig ServicesConfig { get; set; }
    }

    public class Bsc
    {
        [JsonProperty("RpcEndpoint")] public string RpcEndpoint { get; set; }
        [JsonProperty("Enemies")] public List<string> Enemies { get; set; }

        [JsonProperty("Address")] public string Address { get; set; }

        [JsonProperty("ServicesConfig")] public ServicesConfig ServicesConfig { get; set; }
    }

    public class Ply
    {
        [JsonProperty("RpcEndpoint")] public string RpcEndpoint { get; set; }
        [JsonProperty("Enemies")] public List<string> Enemies { get; set; }

        [JsonProperty("Address")] public string Address { get; set; }

        [JsonProperty("ServicesConfig")] public ServicesConfig ServicesConfig { get; set; }
    }
    
    public class Ftm
    {
        [JsonProperty("RpcEndpoint")] public string RpcEndpoint { get; set; }
        [JsonProperty("Enemies")] public List<string> Enemies { get; set; }

        [JsonProperty("Address")] public string Address { get; set; }

        [JsonProperty("ServicesConfig")] public ServicesConfig ServicesConfig { get; set; }
    }

    public class AnalyzerConfig
    {
        [JsonProperty("Heco")] public Heco Heco { get; set; }

        [JsonProperty("Bsc")] public Bsc Bsc { get; set; }

        [JsonProperty("Ply")] public Ply Ply { get; set; }
        
        [JsonProperty("Ftm")] public Ftm Ftm { get; set; }
    }

    public class Root
    {
        [JsonProperty("AnalyzerConfig")] public AnalyzerConfig AnalyzerConfig { get; set; }
    }


    public class TokenListConfig
    {
        public List<string> whitelisted { get; set; }
        public List<string> blacklisted { get; set; }
    }
}