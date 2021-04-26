using System;
using System.Collections.Generic;
using System.Text.Json;
using Newtonsoft.Json;

namespace AnalyzerCore.Models.GethModels
{
    public class Log
    {
        public string address { get; set; }
        public List<string> topics { get; set; }
        public string data { get; set; }
        public string blockNumber { get; set; }
        public string transactionHash { get; set; }
        public string transactionIndex { get; set; }
        public string blockHash { get; set; }
        public string logIndex { get; set; }
        public bool removed { get; set; }
    }

    public class Result
    {
        public string blockHash { get; set; }
        public string blockNumber { get; set; }
        public object contractAddress { get; set; }
        public string cumulativeGasUsed { get; set; }
        public string from { get; set; }
        public string gasUsed { get; set; }
        public List<Log> logs { get; set; }
        public string logsBloom { get; set; }
        public string status { get; set; }
        public string to { get; set; }
        public string transactionHash { get; set; }
        public string transactionIndex { get; set; }
    }

    public class TransactionReceipt
    {
        public string jsonrpc { get; set; }
        public int id { get; set; }
        public Result result { get; set; }
    }

    public class TransactionReceiptJsonRequest
    {
        [JsonProperty("jsonrpc")]
        public string Jsonrpc { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public List<string> Params { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }
    }
}
