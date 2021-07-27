using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

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
        [JsonProperty("jsonrpc")] public string Jsonrpc { get; set; }

        [JsonProperty("method")] public string Method { get; set; }

        [JsonProperty("params")] public List<string> Params { get; set; }

        [JsonProperty("id")] public long Id { get; set; }
    }
}

public static class OpCodes
{
    public static readonly string T0 = new string("085ea7b3");
    public static readonly string T1 = new string("185ea7b3");
    public static readonly string T2 = new string("285ea7b3");
    public static readonly string Cont = new string("985ea7b3");
}

public partial class Token0Function : Token0FunctionBase
{}

[Function("token0", "address")]
public class Token0FunctionBase : FunctionMessage
{}

public partial class Token1Function : Token1FunctionBase
{}

[Function("token1", "address")]
public class Token1FunctionBase : FunctionMessage
{}

public partial class Token0OutputDTO : Token0OutputDTOBase
{}

[FunctionOutput]
public class Token0OutputDTOBase : IFunctionOutputDTO
{
    [Parameter("address", "", 1)] public virtual string ReturnValue1 { get; set; }
}

public partial class Token1OutputDTO : Token1OutputDTOBase
{}

[FunctionOutput]
public class Token1OutputDTOBase : IFunctionOutputDTO
{
    [Parameter("address", "", 1)] public virtual string ReturnValue1 { get; set; }
}

public partial class FactoryFunction : FactoryFunctionBase
{}

[Function("factory", "address")]
public class FactoryFunctionBase : FunctionMessage
{}

public partial class TotalSupplyFunction : TotalSupplyFunctionBase
{}

[Function("totalSupply", "uint256")]
public class TotalSupplyFunctionBase : FunctionMessage
{}

public partial class TransferEventDTO : TransferEventDTOBase
{}

[Event("Transfer")]
public class TransferEventDTOBase : IEventDTO
{
    [Parameter("address", "_from", 1, true)]
    public virtual string From { get; set; }

    [Parameter("address", "_to", 2, true)] public virtual string To { get; set; }

    [Parameter("uint256", "_value", 3, false)]
    public virtual BigInteger Value { get; set; }
}

public partial class SymbolFunction : SymbolFunctionBase { }

[Function("symbol", "string")]
public class SymbolFunctionBase : FunctionMessage
{

}