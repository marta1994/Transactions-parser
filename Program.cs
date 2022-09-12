using ParseKonto;

var sheetManager = new SheetManager();
sheetManager.Init();
var headersManager = new HeadersManager(sheetManager);
var parser = new PdfParser(sheetManager, headersManager);
parser.ParsePdf();