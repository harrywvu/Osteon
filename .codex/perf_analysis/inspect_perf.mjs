import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const path = "C:/Users/CCIS-COD-LAPTOP D8/Downloads/Quest_Perf_Tracker (1).xlsx";
const input = await FileBlob.load(path);
const workbook = await SpreadsheetFile.importXlsx(input);

const overview = await workbook.inspect({
  kind: "workbook,sheet,table,drawing",
  maxChars: 12000,
  tableMaxRows: 100,
  tableMaxCols: 30,
  tableMaxCellChars: 120,
});
console.log(overview.ndjson);

for (const sheet of workbook.worksheets.items) {
  const used = sheet.getUsedRange();
  console.log(`\n--- ${sheet.name} / ${used.address} ---`);
  const detail = await workbook.inspect({
    kind: "table,formula",
    sheetId: sheet.name,
    range: used.address,
    maxChars: 20000,
    tableMaxRows: 200,
    tableMaxCols: 40,
    tableMaxCellChars: 140,
  });
  console.log(detail.ndjson);
}
