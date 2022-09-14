using ParseKonto;

var sheetManager = new SheetManager();
sheetManager.Init();
var headersManager = new HeadersManager(sheetManager);
var valuesWriter = new ValuesWriter(sheetManager, headersManager);
var dbTransactionsparser = new DbTransactionsParser(headersManager, valuesWriter);
var gsuParser = new GsuTransactionsParser(valuesWriter, headersManager);
valuesWriter.ClearValues();
dbTransactionsparser.ParseTransactions();
gsuParser.ParseGsus();