namespace ParseKonto
{
    internal class CurrencyManager
    {
        private readonly SortedDictionary<DateOnly, float> _currency;

        private const string _currencyPathTemplate = "../../../Input data/CURRENCY_EUR_USD_{0}.csv";

        internal CurrencyManager()
        {
            _currency = LoadCurrency();
        }

        private SortedDictionary<DateOnly, float> LoadCurrency()
        {
            var result = new SortedDictionary<DateOnly, float>();
            for (var year = 2020; year <= DateTime.Now.Year; year++)
            {
                var lines = File.ReadAllLines(string.Format(_currencyPathTemplate, year));
                var headersIndexes = GetHeaderIndexes(lines[0]);
                foreach (var line in lines.Skip(1))
                {
                    var vals = line.Split(',');
                    var date = DateOnly.ParseExact(vals[headersIndexes["Date"]], "yyyy-MM-dd");
                    var low = float.Parse(vals[headersIndexes["Low"]].Trim('"'));
                    var high = float.Parse(vals[headersIndexes["High"]].Trim('"'));
                    result.Add(date, (low + high) / 2);
                }
            }
            return result;
        }

        internal float GetCurrencyRatio(DateOnly date)
        {
            var keys = new List<DateOnly>(_currency.Keys);
            var nearestDateInd = -1;
            while (nearestDateInd < 0)
            {
                nearestDateInd = keys.BinarySearch(date);
                date = date.AddDays(1);
            }

            return _currency[keys[nearestDateInd]];
        }

        private Dictionary<string, int> GetHeaderIndexes(string headerLine)
        {
            var headers = headerLine.Split(',');
            var headersIndexes = new Dictionary<string, int>();
            for (var i = 0; i < headers.Length; i++)
            {
                headersIndexes.Add(headers[i], i);
            }
            return headersIndexes;
        }
    }
}
