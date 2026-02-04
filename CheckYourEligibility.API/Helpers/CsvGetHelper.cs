using DocumentFormat.OpenXml.Spreadsheet;


public static class CsvGetHelper
{

    public static List<string> GetColumnHeaders(Row headerRow, SharedStringTable sharedStrings)
    {
        List<string> columnHeaders = [];
        foreach (Cell cell in headerRow.Elements().Skip(1))
        {
            if (cell.CellValue is null) continue;
            var headerName = cell.CellValue.InnerText;
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                headerName = sharedStrings.ChildElements[int.Parse(headerName)].InnerText;
            }
            columnHeaders.Add(headerName.Trim());
        }
        return columnHeaders;
    }

    public static string getCellValueString(Cell cell, SharedStringTable sharedStrings, CellFormat[] cellStyles)
    {
        if (cell.CellValue is null)
            return string.Empty;
        string value = cell.CellValue.InnerText.Trim();

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            // Get shared string value
            value = sharedStrings.ChildElements[int.Parse(value)].InnerText.Trim();
            // If the cell is a date field then convert to standard date format OADate
            CellFormat style = cellStyles[int.Parse(cell.StyleIndex.InnerText)];
            if (style.NumberFormatId == 14)
            {
                value = DateTime.ParseExact(value, "dd/MM/yyyy", null).ToOADate().ToString();
            }
        }
        return value;
    }
}