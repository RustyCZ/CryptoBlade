using Microsoft.Extensions.Options;

namespace CryptoBlade.BackTesting
{
    public class ProtoHistoricalDataStorage : IHistoricalDataStorage
    {
        private readonly IOptions<ProtoHistoricalDataStorageOptions> m_options;

        public ProtoHistoricalDataStorage(IOptions<ProtoHistoricalDataStorageOptions> options)
        {
            m_options = options;
        }

        public Task<HistoricalDayData> ReadAsync(string symbol, DateTime day, CancellationToken cancel = default)
        {
            var fileName = GetSymbolFileName(symbol, day);
            if (!File.Exists(fileName))
            {
                var data = new HistoricalDayData
                {
                    Day = day,
                };
                return Task.FromResult(data);
            }
                
            using var file = File.OpenRead(fileName);
            var dayData = ProtoBuf.Serializer.Deserialize<HistoricalDayData>(file);
            return Task.FromResult(dayData);
        }

        public Task StoreAsync(string symbol, HistoricalDayData dayData, bool flush, CancellationToken cancel = default)
        {
            var symbolDirectory = GetSymbolDirectory(symbol, dayData.Day);
            if (!Directory.Exists(symbolDirectory))
                Directory.CreateDirectory(symbolDirectory);
            var fileName = GetSymbolFileName(symbol, dayData.Day);
            if(File.Exists(fileName))
                File.Delete(fileName);
            using var file = File.OpenWrite(fileName);
            ProtoBuf.Serializer.Serialize(file, dayData);
            return Task.CompletedTask;
        }

        public Task<DateTime[]> FindMissingDaysAsync(string symbol, DateTime start, DateTime end, CancellationToken cancel = default)
        {
            start = start.Date;
            var days = new List<DateTime>();
            var directory = GetSymbolDirectory(symbol, start);
            HashSet<string> files = new HashSet<string>();
            if (Directory.Exists(directory))
            {
                var foundFiles = Directory.GetFiles(directory, "*.pb");
                foreach (var foundFile in foundFiles)
                    files.Add(foundFile);
            }
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                var fileName = GetSymbolFileName(symbol, day);
                if (!files.Contains(fileName))
                    days.Add(day);
            }
            return Task.FromResult(days.ToArray());
        }

        private string GetSymbolFileName(string symbol, DateTime day)
        {
            var fileName = Path.Combine(m_options.Value.Directory, symbol, $"{symbol}_{day:yyyy-MM-dd}.pb");
            return fileName;
        }

        private string GetSymbolDirectory(string symbol, DateTime day)
        {
            var fileName = Path.Combine(m_options.Value.Directory, symbol);
            return fileName;
        }
    }
}