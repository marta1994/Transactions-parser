using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace ParseKonto
{
    internal class HeadersManager
    {
        private readonly SheetManager _sheetManager;

        internal HeadersManager(SheetManager sheetManager)
        {
            _sheetManager = sheetManager;
        }

        internal Dictionary<string, string> UpdateHeaders(IList<string> headers)
        {
            var existingHeaders = new HashSet<string>(GetHeaders().Keys);
            var missingHeaders = headers.Where(h => !existingHeaders.Contains(h)).Select(v => (object)v).ToList();
            if (missingHeaders.Any())
            {
                ValueRange body = new ValueRange();
                body.Values = new List<IList<object>> { missingHeaders };
                body.MajorDimension = "ROWS";
                var request = _sheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.SheetId,
                    $"{SheetManager.TableName}!{(char)('A'+existingHeaders.Count)}1:{(char)('A' + existingHeaders.Count + missingHeaders.Count - 1)}1");
                request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                request.Execute();
            }

            return GetHeaders();
        }

        private Dictionary<string, string> GetHeaders()
        {
            var valRange = _sheetManager.Service.Spreadsheets.Values.Get(
                SheetManager.SheetId, $"{SheetManager.TableName}!A1:1").Execute();
            if (valRange.Values == null) return new Dictionary<string, string>();
            var l = valRange.Values.SelectMany(l => l).ToList();
            var res = new Dictionary<string, string>();
            for(var i = 0; i < l.Count; ++i)
            {
                res[l[i]?.ToString() ?? string.Empty] = ((char)('A'+i)).ToString();
            }
            return res;
        }
    }
}
