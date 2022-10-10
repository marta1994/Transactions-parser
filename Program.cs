using ParseKonto;

var sheetManager = new SheetManager();
sheetManager.Init();
var headersManager = new HeadersManager(sheetManager);
var valuesWriter = new ValuesWriter(sheetManager, headersManager);
var currencyManager = new CurrencyManager();
var dbTransactionsparser = new DbTransactionsParser(headersManager, valuesWriter);
var gsuParser = new GsuTransactionsParser(valuesWriter, headersManager, currencyManager);
var apartmentSheetParser = new ApartmentSheetManager(valuesWriter, headersManager, currencyManager, sheetManager);
valuesWriter.ClearValues();
dbTransactionsparser.ParseTransactions();
gsuParser.ParseGsus();
apartmentSheetParser.Parse();