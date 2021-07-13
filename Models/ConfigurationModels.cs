using System.Collections.Generic;

namespace AnalyzerCore.Models
{
    public class ConfigurationModels
    {
    }

    public class AnalyzerConfig
    {
        public string PlyAddress { get; set; }
        public string BscAddress { get; set; }
        public string HecoAddress { get; set; }
        public List<string> PlyEnemies { get; set; }
        public List<string> BscEnemies { get; set; }
        public List<string> HecoEnemies { get; set; }
    }

    public class ServicesConfig
    {
        public bool BscEnabled { get; set; }
        public bool PlyEnabled { get; set; }
        public bool HecoEnabled { get; set; }
        public bool PlyPendingEnabled { get; set; }
        public int MaxParallelism { get; set; }
    }

    public class TokenListConfig
    {
        public List<string> whitelisted { get; set; }
        public List<string> blacklisted { get; set; }
    }
}