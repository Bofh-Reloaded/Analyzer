using System;
using System.Collections;
using System.Collections.Generic;
using AnalyzerCore.Models.BscScanModels;

namespace AnalyzerCore.Models
{
    public class ModelsCollection
    {
        public ModelsCollection()
        {
        }
    }

    public class Range : IEnumerable
    {
        public string rangeName { get; set; }
        public List<Result> trxInRange { get; set; }

        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public class Address
    {
        public string address { get; set; }
        public List<Result> transactions { get; set; }

        public List<Result> GetTransactions(int numberOfBlocks)
        {
            return this.transactions;
        }

    }
}
