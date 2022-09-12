using Google.Apis.Sheets.v4.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ParseKonto
{
    internal class PdfParser
    {
        private const string _statementsPath = "../../../Statements";
        private readonly SheetManager _sheetManager;
        private readonly HeadersManager _headersManager;
        private const string _dateFormat = "yyyy-MM-dd";

        internal PdfParser(SheetManager sheetManager, HeadersManager headersManager)
        {
            _sheetManager = sheetManager;
            _headersManager = headersManager;
        }

        internal void ParsePdf()
        {
            var parsed = ParsePdfInternal();
            var headers = _headersManager.UpdateHeaders(parsed[0].Keys.ToList());
            parsed = DeleteDuplicates(headers, parsed);
            if (!parsed.Any()) return;

            var allValues = _sheetManager.Service.Spreadsheets.Values.Get(SheetManager.SheetId,
               $"{SheetManager.TableName}!A1:{headers.Last().Value}").Execute();
            var occupiedCells = allValues.Values?.Count ?? 0;

            var batchUpdateRequest = new BatchUpdateValuesRequest();
            batchUpdateRequest.ValueInputOption = "RAW";
            batchUpdateRequest.Data = new List<ValueRange>();

            foreach (var header in headers)
            {
                if (!parsed[0].ContainsKey(header.Key)) continue;
                Console.WriteLine($"Writing values of '{header.Key}' to {header.Value} column...");
                ValueRange range = new ValueRange();
                range.Values = new List<IList<object>> { parsed.Select(p => p[header.Key]).ToList() };
                range.Range = $"{SheetManager.TableName}!{header.Value}{occupiedCells + 1}:{header.Value}";
                range.MajorDimension = "COLUMNS";
                batchUpdateRequest.Data.Add(range);
            }


            var result = _sheetManager.Service.Spreadsheets.Values.BatchUpdate(batchUpdateRequest,
              SheetManager.SheetId).Execute();
        }

        private List<Dictionary<string, object>> DeleteDuplicates(
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
            for(var i = 0; i < resp.ValueRanges[0].Values.Count; i++)
            {
                trSet.Add($"{resp.ValueRanges[0].Values[i][0]}:{resp.ValueRanges[1].Values[i][0]}");
            }
            return data.Where(x =>  !trSet.Contains($"{x[Headers.TransactionType]}:{x[Headers.Source]}")).ToList();
        }

        private List<Dictionary<string, object>> ParsePdfInternal()
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            DirectoryInfo d = new DirectoryInfo(_statementsPath);
            FileInfo[] Files = d.GetFiles("*.txt");
            foreach (var f in Files)
            {
                string text = File.ReadAllText(f.FullName);
                var dates = GetStatementDates(text);
                var transactions = GetTransactions(text);
                foreach (var trans in transactions)
                {
                    var dict = new Dictionary<string, object>();
                    result.Add(dict);
                    dict.Add(Headers.TransactionType, "Deutsche Bank Account");
                    dict.Add(Headers.Source, f.Name.Substring(0, f.Name.Length - 4));
                    dict.Add(Headers.SourceDateFrom, dates.Item1.ToString(_dateFormat));
                    dict.Add(Headers.SourceDateTo, dates.Item2.ToString(_dateFormat));
                    ParseTransaction(trans, dict, dates.Item1.Year);
                }
                var sum = GetNewSaldo(text);
                var calculatedSum = result.Sum(v => (float)v[Headers.Sum]);
                if (Math.Abs(sum - calculatedSum) > 0.1)
                {
                    Console.Write($"Actual sum {sum} is not the same as calsulted sum {calculatedSum} after '{f.Name}'.");
                }
            }
            return result;
        }

        private float GetNewSaldo(string text)
        {
            string marker = "Neuer Saldo";
            int markerInd = text.IndexOf(marker);
            if (markerInd == -1) throw new Exception("No new saldo found!");
            int nextId = markerInd + marker.Length;
            while (text[nextId] != '+') nextId++;
            markerInd = nextId;
            nextId++;
            while (!char.IsDigit(text[nextId])) nextId++;
            while (char.IsDigit(text[nextId]) || text[nextId] == '.' || text[nextId] == ',') nextId++;
            var numStr = text.Substring(markerInd, nextId - markerInd);
            numStr = numStr.Replace(" ", "").Replace(".", "").Replace(",", "");
            return float.Parse(numStr) / 100;
        }

        private void ParseTransaction(string trans, Dictionary<string, object> vals, int year)
        {
            var date1Str = trans.Substring(0, 6) + year.ToString();
            var date2Str = trans.Substring(7, 6) + year.ToString();
            var format = "dd.MM.yyyy";
            var prov = CultureInfo.InvariantCulture;
            vals.Add(Headers.Date1, DateTime.ParseExact(date1Str, format, prov).ToString(_dateFormat));
            vals.Add(Headers.Date2, DateTime.ParseExact(date2Str, format, prov).ToString(_dateFormat));
            var plus = trans.LastIndexOf("+ ");
            if (plus < 0) plus = trans.Length;
            var minus = trans.LastIndexOf("- ");
            if (minus < 0) minus = trans.Length;
            var signInd = Math.Max(plus, minus);
            if (signInd == trans.Length)
                signInd = Math.Min(plus, minus);
            if (signInd == trans.Length)
                throw new Exception("No sum in transaction.");

            var desc = trans.Substring(13, signInd - 13).Replace('\n', ' ');
            RegexOptions options = RegexOptions.None;
            Regex regex = new Regex("[ ]{2,}", options);
            desc = regex.Replace(desc, " ");
            vals.Add(Headers.Description, desc);

            var sumStr = trans.Substring(signInd).Replace(" ", "").Replace(".", "").Replace(",", "");
            vals.Add(Headers.Sum, float.Parse(sumStr) / 100);
        }

        private List<string> GetTransactions(string text)
        {
            text = text.Replace("FiliaInummer", "Filialnummer");
            var startMark = "IBAN BIC";
            var startInd = text.IndexOf(startMark);
            var endMark = "\nFilialnummer";
            var endIndex = text.IndexOf(endMark);
            text = text.Substring(startInd + startMark.Length, endIndex - startInd - startMark.Length);
            var prevInd = 0;
            var res = new List<string>();
            while (true)
            {
                var dt = NextDateIndex(text, prevInd);
                if (dt == -1) break;
                prevInd = NextSumEnds(text, dt);
                res.Add(text.Substring(dt, prevInd - dt));
            }
            return res;

        }

        private int NextSumEnds(string text, int ind)
        {
            while (true)
            {
                var plus = text.IndexOf("+ ", ind);
                if (plus < 0) plus = text.Length;
                var minus = text.IndexOf("- ", ind);
                if (minus < 0) minus = text.Length;
                var signInd = Math.Min(plus, minus);
                if (signInd == text.Length) return -1;
                var id = signInd;
                while (text[id] != '\n')
                {
                    id--;
                    if (text[id] != ' ' && text[id] != '\n') break;
                }
                if (text[id] != '\n')
                {
                    ind = signInd + 1;
                    continue;
                }
                int res = signInd + 2;
                if (res >= text.Length) 
                    return -1;
                if (!char.IsDigit(text[res]))
                {
                    ind = signInd + 1;
                    continue;
                }
                while (res < text.Length && (char.IsDigit(text[res])
                    || text[res] == '.' || text[res] == ',')) res++;
                return res;
            }
        }

        private int NextDateIndex(string text, int start)
        {
            while (start < text.Length && !IsDate(text, start)) start++;
            return start < text.Length ? start : -1;
        }

        private bool IsDate(string text, int ind)
        {
            return text.Length > ind + 13 && IsDigitDot(text, ind) && IsDigitDot(text, ind + 3)
                && text[ind+6]== ' ' && IsDigitDot(text, ind+7) && IsDigitDot(text, ind + 10);
        }

        private bool IsDigitDot(string text, int ind)
        {
            return text.Length > ind + 3 && char.IsDigit(text[ind]) && char.IsDigit(text[ind + 1])
                && text[ind + 2] == '.';
        }

        private Tuple<DateTime, DateTime> GetStatementDates(string text)
        {
            var spot = "Kontoauszug vom ";
            var ind = text.IndexOf(spot);

            var dateFrom = text.Substring(ind + spot.Length, 10);
            var dateTo = text.Substring(ind + spot.Length + 15, 10);
            var format = "dd.MM.yyyy";
            var prov = CultureInfo.InvariantCulture;
            return Tuple.Create(DateTime.ParseExact(dateFrom, format, prov), DateTime.ParseExact(dateTo, format, prov)); 
        }
    }
}
