using System;
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
        
        public async Task<CurrentBlock> GetCurrentBlock()
        {
            var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3000;
            var requestUri = $"api?module=block&action=getblocknobytime&timestamp={unixTimestamp}&closest=before&apikey={apiToken}";
            HttpResponseMessage response = await Client.GetAsync(requestUri: requestUri);
            CurrentBlock currentBlock = JsonSerializer.Deserialize<CurrentBlock>(
                await response.Content.ReadAsStringAsync()
                );
            return currentBlock;
        }

        public async Task<Transaction> RetrieveTransactionsAsync(string address, string startBlock, string endBlock)
        {
            var trx = new Transaction();
            string content = "";
            try
            {
                var requestUri = $"api?module=account&action=txlist&address={address}&startblock={startBlock}&endblock={endBlock}&sort=asc&apikey={apiToken}";
                HttpResponseMessage response = await Client.GetAsync(requestUri: requestUri);
                content = await response.Content.ReadAsStringAsync();
                trx = JsonSerializer.Deserialize<Transaction>(content);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
                log.Error(content);
                throw ex;
            }
            return trx;
        }
    }
}
