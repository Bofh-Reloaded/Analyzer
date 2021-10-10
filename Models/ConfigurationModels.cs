using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace AnalyzerCore.Models
{
    public class ConfigurationModels
    {}

    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class DataCollector
    {
        public bool Enabled { get; set; }
    }

    public class AnalyzerService
    {
        public bool Enabled { get; set; }
        public string BotToken { get; set; }
        public string ChatId { get; set; }
        public int BlockDurationTime { get; set; }
    }

    public class TokenAnalyzer
    {
        public bool Enabled { get; set; }
        public string TokenFileName { get; set; }
        public string BotToken { get; set; }
        public string ChatId { get; set; }
        public string BaseUri { get; set; }
    }

    public class NewTokenService
    {
        public bool Enabled { get; set; }
    }

    public class ServicesConfig
    {
        public DataCollector DataCollector { get; set; }
        public AnalyzerService AnalyzerService { get; set; }
        public TokenAnalyzer TokenAnalyzer { get; set; }
        public NewTokenService NewTokenService { get; set; }
        public int MaxParallelism { get; set; }
    }

    public class AnalyzerConfig
    {
        public string ChainName { get; set; }
        public List<string> RpcEndpoints { get; set; }
        public int RpcPort { get; set; }
        public int WssPort { get; set; }
        public List<string> Enemies { get; set; }
        public List<string> Wallets { get; set; }
        public ServicesConfig ServicesConfig { get; set; }
    }

    public class Root
    {
        public AnalyzerConfig AnalyzerConfig { get; set; }
    }


    public class TokenListConfig
    {
        public List<string> whitelisted { get; set; }
        public List<string> blacklisted { get; set; }
    }
}