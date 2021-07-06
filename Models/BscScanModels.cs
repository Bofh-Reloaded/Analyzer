﻿using System;
using System.Collections.Generic;

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AnalyzerCore.Models.BscScanModels
{
    public class Result
    {
        public string blockNumber { get; set; }
        public string timeStamp { get; set; }
        public string hash { get; set; }
        public string nonce { get; set; }
        public string blockHash { get; set; }
        public string transactionIndex { get; set; }
        public string from { get; set; }
        public string to { get; set; }
        public string value { get; set; }
        public string gas { get; set; }
        public string gasPrice { get; set; }
        public string isError { get; set; }
        public string txreceipt_status { get; set; }
        public string input { get; set; }
        public string contractAddress { get; set; }
        public string cumulativeGasUsed { get; set; }
        public string gasUsed { get; set; }
        public string confirmations { get; set; }

        public double getGasPrice()
        {
            return double.Parse(gasPrice) / Math.Pow(10, 9);
        }
    }

    public class Transaction
    {
        public string status { get; set; }
        public string message { get; set; }
        public List<Result> result { get; set; }
    }

    public class CurrentBlock
    {
        public string status { get; set; }
        public string message { get; set; }
        public string result { get; set; }
    }
}