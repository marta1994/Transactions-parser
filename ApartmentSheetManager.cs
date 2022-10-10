using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParseKonto
{
    internal class ApartmentSheetManager
    {
        private readonly ValuesWriter _valuesWriter;
        private readonly HeadersManager _headersManager;
        private readonly CurrencyManager _currencyManager;
        private readonly SheetManager _sheetManager;
        private const string _sheetId = "1aP7gihe7zRl1kI7Dc5Z8HnMg0c8qksvw91WhSE5qFnI";
        private const string _sheetName = "Main";

        internal ApartmentSheetManager(ValuesWriter valuesWriter, HeadersManager headersManager, CurrencyManager currencyManager, SheetManager sheetManager)
        {
            _valuesWriter = valuesWriter;
            _headersManager = headersManager;
            _currencyManager = currencyManager;
            _sheetManager = sheetManager;
        }

        internal void Parse()
        {
            var parsed = ParseInternal();
            var headers = _headersManager.UpdateHeaders(parsed[0].Keys.ToList());
            parsed = _valuesWriter.DeleteDuplicates(headers, parsed);
            _valuesWriter.WriteValues(parsed);
        }

        private List<Dictionary<string, object>> ParseInternal()
        {
            var result = new List<Dictionary<string, object>>();
            var getReq = _sheetManager.Service.Spreadsheets.Values.BatchGet(_sheetId);
            getReq.Ranges = new Google.Apis.Util.Repeatable<string>(new[]
            {
                $"{_sheetName}!A2:A",
                $"{_sheetName}!C2:C",
                $"{_sheetName}!D2:D"
            });
            var res = getReq.Execute();
            for (var i = 0; i < res.ValueRanges[0].Values.Count; i++)
            {
                var vals = new Dictionary<string, object>();
                vals.Add(Headers.TransactionType, "Rental apartment: Zelena");
                vals.Add(Headers.Source, "Apartment spreadsheet");
                DateOnly date = DateOnly.ParseExact(res.ValueRanges[1].Values[i][0].ToString(), "yyyy-MM-dd");
                var sum = float.Parse(res.ValueRanges[0].Values[i][0].ToString()) / _currencyManager.GetCurrencyRatio(date);
                vals.Add(Headers.Sum, sum);
                vals.Add(Headers.Date, res.ValueRanges[1].Values[i][0]);
                vals.Add(Headers.Description, res.ValueRanges[2].Values[i][0]);
                result.Add(vals);
            }

            return result;
        }
    }
}
