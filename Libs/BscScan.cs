﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using AnalyzerCore.Models.BscScanModels;
using System.Text.Json;
using log4net;
using System.Reflection;

namespace AnalyzerCore.Libs
{
    public class BscScan
    {
        private static readonly HttpClient Client = new HttpClient();
        static readonly string apiToken = "NAP33JZKFUWHNC9K8DAXA7F3FJVDN4KIME";
        private static readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );

        public BscScan()

        {
            Client.BaseAddress = new Uri("https://api.bscscan.com/");
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
                );
        }
        
        public async Task<string> GetCurrentBlock()
        {
            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var requestUri = $"api?module=block&action=getblocknobytime&timestamp={unixTimestamp}&closest=before&apikey={apiToken}";
            HttpResponseMessage response = await Client.GetAsync(requestUri: requestUri);
            CurrentBlock currentBlock = JsonSerializer.Deserialize<CurrentBlock>(
                await response.Content.ReadAsStringAsync()
                );
            return currentBlock.result;
        }

        public async Task<Transaction> RetrieveTransactionsAsync(string address, string startBlock, string endBlock)
        {
            var trx = new Transaction();

            try
            {
                var requestUri = $"api?module=account&action=txlist&address={address}&startblock={startBlock}&endblock={endBlock}&sort=asc&apikey={apiToken}";
                HttpResponseMessage response = await Client.GetAsync(requestUri: requestUri);
                trx = JsonSerializer.Deserialize<Transaction>(await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }

            return trx;
        }
    }
}
