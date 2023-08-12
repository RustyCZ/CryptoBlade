using static System.Formats.Asn1.AsnWriter;
using System;

namespace CryptoBlade.BackTesting
{
    public class HistoricalTradesStorageOptions
    { 
        public string Directory { get; set; } = "HistoricalData";
    }
}
