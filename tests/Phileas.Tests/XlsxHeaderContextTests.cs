/*
 * Copyright 2026 Philterd, LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Phileas.Model;
using Phileas.Services.Office;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     Exercises the prototype "prefix-and-remap" header context for the xlsx redactor: the column header is
///     prepended to a data cell's text as detection context, and the detected spans are mapped back onto the
///     cell so only the cell — not the header prefix — is rewritten.
/// </summary>
public sealed class XlsxHeaderContextTests
{
    // A stand-in detector that only fires when it can see BOTH the "SSN" header and the value "999" in its
    // input. That makes it context-dependent: it redacts the data cell only when the header is supplied.
    private static readonly Func<string, TextFilterResult> ContextDependentFilter = input =>
    {
        var index = input.IndexOf("999", StringComparison.Ordinal);
        if (index < 0 || !input.Contains("SSN", StringComparison.Ordinal))
            return new TextFilterResult(input, new List<Span>());

        var span = Span.Make(index, index + 3, FilterType.Other, "ctx", 1.0, "999", "***",
            string.Empty, false, true, null, 0);
        var filtered = input[..index] + "***" + input[(index + 3)..];
        return new TextFilterResult(filtered, new List<Span> { span });
    };

    [Fact]
    public void WithoutHeaderContext_ContextDependentDetectorDoesNotFire()
    {
        var xlsx = BuildSingleColumnXlsx("SSN", "999"); // A1 header, A2 data

        var (_, spans) = XlsxRedactor.Redact(xlsx, ContextDependentFilter); // useHeaderContext defaults false

        // The data cell "999" is scanned alone (no "SSN"), so nothing is detected.
        Assert.Empty(spans);
        Assert.Equal("999", ReadCell(BuildSingleColumnXlsx("SSN", "999"), "A2"));
    }

    [Fact]
    public void WithHeaderContext_DetectsUsingHeader_AndRemapsSpanOntoCell()
    {
        var xlsx = BuildSingleColumnXlsx("SSN", "999");

        var (redacted, spans) = XlsxRedactor.Redact(xlsx, ContextDependentFilter, useHeaderContext: true);

        // The detector saw "SSN: 999" and fired; the span is remapped onto the cell's own text (offsets 0..3),
        // NOT the "SSN: " prefix.
        Assert.Single(spans);
        Assert.Equal(0, spans[0].CharacterStart);
        Assert.Equal(3, spans[0].CharacterEnd);
        Assert.Equal("999", spans[0].Text);

        // Only the data cell was rewritten; the header cell is untouched.
        Assert.Equal("***", ReadCell(redacted, "A2"));
        Assert.Equal("SSN", ReadCell(redacted, "A1"));
    }

    private static byte[] BuildSingleColumnXlsx(params string[] columnAValues)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            for (var r = 0; r < columnAValues.Length; r++)
            {
                var cell = new Cell
                {
                    CellReference = $"A{r + 1}",
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(columnAValues[r]))
                };
                sheetData.Append(new Row(cell) { RowIndex = (uint)(r + 1) });
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1"
            });
            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static string ReadCell(byte[] xlsx, string reference)
    {
        using var stream = new MemoryStream(xlsx);
        using var document = SpreadsheetDocument.Open(stream, isEditable: false);
        var workbookPart = document.WorkbookPart!;
        var cell = workbookPart.WorksheetParts.First().Worksheet
            .Descendants<Cell>().First(c => c.CellReference == reference);

        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.Text?.Text ?? string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            var table = workbookPart.SharedStringTablePart!.SharedStringTable;
            return table.ElementAt(int.Parse(cell.CellValue!.Text)).InnerText;
        }

        return cell.CellValue?.Text ?? string.Empty;
    }
}
