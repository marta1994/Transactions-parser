using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

namespace ParseKonto
{
    internal class SheetManager
    {
        private const string _sheetId = "14cwEx5gSjxMF1UeEOHpH6CdG2uiD61SoELZh-4zZ9sE";
        private SheetsService _service;

        internal void Init()
        {
            string[] Scopes = { SheetsService.Scope.Spreadsheets };
            string ApplicationName = "Google Sheets API .NET Quickstart";

            UserCredential credential;
            // Load client secrets.
            using (var stream =
                   new FileStream("../../../credentials.json", FileMode.Open, FileAccess.Read))
            {
                /* The file token.json stores the user's access and refresh tokens, and is created
                 automatically when the authorization flow completes for the first time. */
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Google Sheets API service.
            _service = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });
        }

        internal SheetsService Service => _service;

        internal static string SheetId => _sheetId;

        internal static string TableName => "Transactions";
    }
}
