using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParseKonto
{
    internal class ValuesWriter
    {
        private readonly SheetManager _sheetManager;
        private readonly HeadersManager _headersManager;

        internal ValuesWriter(SheetManager sheetManager, HeadersManager headersManager)
        {
            _sheetManager = sheetManager;
            _headersManager = headersManager;
        }

        internal void ClearValues()
        {
            var clearRequest = new ClearValuesRequest();
            _sheetManager.Service.Spreadsheets.Values.Clear(clearRequest, SheetManager.SheetId, $"{SheetManager.TableName}!A1:Z").Execute();
        }

        internal void WriteValues(IList<Dictionary<string, object>> values)
        {
            if (!values.Any()) return;
            var headers = _headersManager.GetHeaders();
            var allValues = _sheetManager.Service.Spreadsheets.Values.Get(SheetManager.SheetId,
               $"{SheetManager.TableName}!A1:{headers.Last().Value}").Execute();
            var occupiedCells = allValues.Values?.Count ?? 0;

            var batchUpdateRequest = new BatchUpdateValuesRequest
            {
                ValueInputOption = "RAW",
                Data = new List<ValueRange>()
            };

            foreach (var header in headers)
            {
                if (!values[0].ContainsKey(header.Key)) continue;
                Console.WriteLine($"Writing values of '{header.Key}' to {header.Value} column...");
                ValueRange range = new ValueRange();
                range.Values = new List<IList<object>> { values.Select(p => p[header.Key]).ToList() };
                range.Range = $"{SheetManager.TableName}!{header.Value}{occupiedCells + 1}:{header.Value}";
                range.MajorDimension = "COLUMNS";
                batchUpdateRequest.Data.Add(range);
            }

            var result = _sheetManager.Service.Spreadsheets.Values.BatchUpdate(batchUpdateRequest,
              SheetManager.SheetId).Execute();
        }

        internal List<Dictionary<string, object>> DeleteDuplicates(
            Dictionary<string, string> headers, List<Dictionary<string, object>> data)
        {
            var typeCol = headers[Headers.TransactionType];
            var sourceCol = headers[Headers.Source];
            var batchGet = _sheetManager.Service.Spreadsheets.Values.BatchGet(SheetManager.SheetId);
            batchGet.Ranges = new Google.Apis.Util.Repeatable<string>(new List<string> {
            $"{SheetManager.TableName}!{typeCol}2:{typeCol}", $"{SheetManager.TableName}!{sourceCol}2:{sourceCol}"});
            var resp = batchGet.Execute();
            var trSet = new HashSet<string>();
            if (resp.ValueRanges[0].Values == null || resp.ValueRanges[1].Values == null)
            {
                return data;
            }
            for (var i = 0; i < resp.ValueRanges[0].Values.Count; i++)
            {
                trSet.Add($"{resp.ValueRanges[0].Values[i][0]}:{resp.ValueRanges[1].Values[i][0]}");
            }
            return data.Where(x => !trSet.Contains($"{x[Headers.TransactionType]}:{x[Headers.Source]}")).ToList();
        }

    }
}
