using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParseKonto
{
    internal class GsuTransactionsParser
    {
        private struct GsuTransaction
        {
            internal float EurPricePerGsu { get; set; }
            internal float GsuCount { get; set; }
            internal DateOnly Date { get; set; }

        }

        private readonly ValuesWriter _valuesWriter;
        private readonly HeadersManager _headersManager;
        private readonly SortedDictionary<DateOnly, float> _currency;
        private const string _withdrawalPath = "../../../Input data/GSU Withdrawals Report.csv";
        private const string _releasesPath = "../../../Input data/GSU Releases Report.csv";
        private const string _currencyPathTemplate = "../../../Input data/CURRENCY_EUR_USD_{0}.csv";
        private const string _dateCol = "Date";
        private const string _priceCol = "Price";
        private const string _quantityCol = "Quantity";
        private const string _netAmountCol = "Net Amount";
        private const string _vestDateCol = "Vest Date";
        private const string _netShareCol = "Net Share Proceeds";
        private const string _inDateFormat = "dd-MMM-yyyy";
        private const string _outDateFormat = "yyyy-MM-dd";
        private const double _eps = 0.0001;

        internal GsuTransactionsParser(ValuesWriter valuesWriter, HeadersManager headersManager)
        {
            _valuesWriter = valuesWriter;
            _headersManager = headersManager;
            _currency = LoadCurrency();
        }

        internal void ParseGsus()
        {
            var parsed = ParseInternal();
            var headers = _headersManager.UpdateHeaders(parsed[0].Keys.ToList());
            parsed = _valuesWriter.DeleteDuplicates(headers, parsed);
            _valuesWriter.WriteValues(parsed);
        }

        private List<Dictionary<string, object>> ParseInternal()
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            var sharePrices = ParseReleases(result);
            ParseWithdrawals(result, sharePrices);
            return result;
        }

        private SortedDictionary<DateOnly, float> LoadCurrency()
        {
            var result = new SortedDictionary<DateOnly, float>();
            for (var year = 2020; year <= DateTime.Now.Year; year++)
            {
                var lines = File.ReadAllLines(string.Format(_currencyPathTemplate, year));
                var headersIndexes = GetHeaderIndexes(lines[0]);
                foreach(var line in lines.Skip(1))
                {
                    var vals = line.Split(',');
                    var date = DateOnly.ParseExact(vals[headersIndexes["Date"]], "MM/dd/yyyy");
                    var low = float.Parse(vals[headersIndexes["Low"]].Trim('"'));
                    var high = float.Parse(vals[headersIndexes["High"]].Trim('"'));
                    result.Add(date, (low + high) / 2);
                }
            }
            return result;
        }

        private void ParseWithdrawals(List<Dictionary<string, object>> result, List<GsuTransaction> sharePrices)
        {
            var lines = File.ReadAllLines(_withdrawalPath);
            var headersIndexes = GetHeaderIndexes(lines[0]);
            int currentVestInd = 0;
            float sharesUsed = 0;
            DateOnly splitDate = new DateOnly(2022, 07, 19);
            foreach (var line in lines.Skip(1).Reverse())
            {
                var vals = line.Split(',');
                Dictionary<string, object> values = new Dictionary<string, object>();
                values.Add(Headers.TransactionType, "GSU");
                values.Add(Headers.Source, FileName(_withdrawalPath));
                var prov = CultureInfo.InvariantCulture;
                var date = DateOnly.ParseExact(vals[headersIndexes[_dateCol]], _inDateFormat, prov);
                values.Add(Headers.Date, date.ToString(_outDateFormat));
                var gsuNumber = float.Parse(vals[headersIndexes[_quantityCol]].Trim('"'));
                gsuNumber = Math.Abs(date < splitDate ? gsuNumber * 20 : gsuNumber);
                values.Add(Headers.GsuNumber, gsuNumber);
                var pricePerGsu = float.Parse(vals[headersIndexes[_priceCol]].Trim('"').Trim('$'));
                pricePerGsu = date < splitDate ? pricePerGsu / 20 : pricePerGsu;
                values.Add(Headers.PricePerGsu, pricePerGsu);
                var netAmount = float.Parse(vals[headersIndexes[_netAmountCol]].Trim('"').Trim('$'));
                values.Add(Headers.UsdSum, netAmount);
                var usdToEur = GetCurrencyRatio(date);
                var eurSale = netAmount / usdToEur;
                float vestPrice = 0;
                float vestCount = 0;
                while (vestCount < gsuNumber - _eps)
                {
                    var restCount = gsuNumber - vestCount;
                    if (restCount < sharePrices[currentVestInd].GsuCount-sharesUsed + _eps)
                    {
                        vestPrice += restCount * sharePrices[currentVestInd].EurPricePerGsu;
                        vestCount += restCount;
                        sharesUsed += restCount;
                        if (sharesUsed > sharePrices[currentVestInd].GsuCount -_eps)
                        {
                            sharesUsed = 0;
                            currentVestInd++;
                        }
                        break;
                    }
                    vestPrice += (sharePrices[currentVestInd].GsuCount - sharesUsed) * sharePrices[currentVestInd].EurPricePerGsu;
                    vestCount += sharePrices[currentVestInd].GsuCount - sharesUsed;
                    sharesUsed = 0;
                    currentVestInd++;
                }
                var profitEur = eurSale - vestPrice;
                values.Add(Headers.Sum, profitEur);
                result.Add(values);
            }
        }

        private List<GsuTransaction> ParseReleases(List<Dictionary<string, object>> result)
        {
            var sharePrices = new List<GsuTransaction>();
            var lines = File.ReadAllLines(_releasesPath);
            var headersIndexes = GetHeaderIndexes(lines[0]);
            
            foreach(var line in lines.Skip(1))
            {
                var vals = line.Split(',');
                Dictionary<string, object> values = new Dictionary<string, object>();
                values.Add(Headers.TransactionType, "GSU");
                values.Add(Headers.Source, FileName(_releasesPath));
                var prov = CultureInfo.InvariantCulture;
                var date = DateOnly.ParseExact(vals[headersIndexes[_vestDateCol]], _inDateFormat, prov);
                values.Add(Headers.Date, date.ToString(_outDateFormat));
                var gsuNumber = float.Parse(vals[headersIndexes[_netShareCol]]);
                values.Add(Headers.GsuNumber, gsuNumber);
                var pricePerGsu = float.Parse(vals[headersIndexes[_priceCol]].Trim('$'));
                values.Add(Headers.PricePerGsu, pricePerGsu);
                values.Add(Headers.UsdSum, pricePerGsu * gsuNumber);
                var usdToEur = GetCurrencyRatio(date);
                sharePrices.Add(new GsuTransaction
                {
                    EurPricePerGsu = pricePerGsu / usdToEur,
                    GsuCount = gsuNumber,
                    Date = date,
                });
                values.Add(Headers.Sum, gsuNumber * pricePerGsu / usdToEur);
                result.Add(values);

            }
            sharePrices = sharePrices.OrderBy(p => p.Date).ToList();
            return sharePrices;
        }

        private float GetCurrencyRatio(DateOnly date)
        {
            var keys = new List<DateOnly>(_currency.Keys);
            var nearestDateInd = -1;
            while(nearestDateInd < 0)
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

        private string FileName(string path) => path.Split('/').Last().Split('.')[0];
    }
}
