using NUnit.Framework;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HtmlToOpenXml.Tests
{
    /// <summary>
    /// Tests for <c>table</c> or <c>pre</c>.
    /// </summary>
    [TestFixture]
    public class TableTests : HtmlConverterTestBase
    {
        [TestCase("<table><tr></tr></table>", Description = "Row with no cells")]
        [TestCase("<table></table>", Description = "No rows")]
        public void IgnoreEmptyTable(string html)
        {
            var elements = converter.Parse(html);
            Assert.That(elements, Is.Empty);
        }

        [Test(Description = "Empty cell should generate an empty Paragraph")]
        public void ParseEmptyCell()
        {
            var elements = converter.Parse(@"<table><tr><td></td></tr></table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());

            var rows = elements[0].Elements<TableRow>();
            Assert.That(rows.Count(), Is.EqualTo(1));
            var cells = rows.First().Elements<TableCell>();
            Assert.That(cells.Count(), Is.EqualTo(1));
            Assert.That(cells.First().HasChild<Paragraph>(), Is.True);
            Assert.That(cells.First().Count(c => c is not TableCellProperties), Is.EqualTo(1));
        }

        [Test(Description = "Second row does not contains complete number of cells")]
        public void ParseRowWithNoCell()
        {
            var elements = converter.Parse(@"<table>
                <tr><td>Cell 1.1</td><td>Cell 1.2</td></tr>
                <tr><td>Cell 2.1</td></tr>
                <tr><!--no cell!--></tr>
            </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var rows = elements[0].Elements<TableRow>();
            Assert.That(rows.Count(), Is.EqualTo(2), "Row with no cells should be skipped");
            Assert.That(rows.Select(r => r.Elements<TableCell>().Count()), 
                Has.All.EqualTo(2),
                "All rows should have the same number of cells");
        }

        [Test(Description = "Respect the order header-body-footer even if provided disordered")]
        public void ParseDisorderedTableParts ()
        {
            // table parts should be reordered
            var elements = converter.Parse(@"<table>
                <tbody><tr><td>Body</td></tr></tbody>
                <thead><tr><td>Header</td></tr></thead>
                <tfoot><tr><td>Footer</td></tr></tfoot>
            </table>");

            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0], Is.TypeOf(typeof(Table)));

            var rows = elements[0].Elements<TableRow>();
            Assert.That(rows.Count(), Is.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(rows.ElementAt(0).InnerText, Is.EqualTo("Header"));
                Assert.That(rows.ElementAt(1).InnerText, Is.EqualTo("Body"));
                Assert.That(rows.ElementAt(2).InnerText, Is.EqualTo("Footer"));
            });
        }

        [TestCase(2u, 2)]
        [TestCase(1u, null)]
        [TestCase(0u, null)]
        public void ParseColSpan(uint colSpan, int? expectedColSpan)
        {
            var elements = converter.Parse(@$"<table>
                    <tr><th colspan=""{colSpan}"">Cell 1.1</th></tr>
                    <tr>{("<td>Cell</td>").Repeat(Math.Max(1, colSpan))}</tr>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var rows = elements[0].Elements<TableRow>();
            Assert.That(rows.Count(), Is.EqualTo(2));

            Assert.Multiple(() =>
            {
                Assert.That(rows.First().GetFirstChild<TableCell>()?
                    .TableCellProperties?.GetFirstChild<GridSpan>()?.Val?.Value, Is.EqualTo(expectedColSpan),
                    $"Expected GridSpan={expectedColSpan}");
                Assert.That(rows.First().Elements<TableCell>().Count(), Is.EqualTo(1),
                    "1st row should contain only 1 cell");
                Assert.That(rows.Last().Elements<TableCell>().Count(), Is.EqualTo(Math.Max(1, colSpan)),
                    $"2nd row should contains {Math.Max(1, colSpan)} cells");
            });
        }

        [Test(Description = "rowSpan=0 should extend on all rows")]
        public void ParseRowSpanZero()
        {
            var elements = converter.Parse(@"<table>
                <tbody>
                    <tr><td rowspan=""0"">Cell 1.1</td><td>Cell 1.2</td><td>Cell 1.3</td></tr>
                    <tr><td>Cell 2.2</td><td>Cell 2.3</td></tr>
                    <tr><td>Cell 3.2</td><td>Cell 3.3</td></tr>
                </tbody>
                <tfoot>
                    <tr><td>Cell 4.1</td><td>Cell 4.2</td><td>Cell 4.3</td></tr>
                </tfoot>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var rows = elements[0].Elements<TableRow>().ToArray();
            Assert.That(rows, Has.Length.EqualTo(4));
            Assert.Multiple(() =>
            {
                Assert.That(rows.Select(r => r.Elements<TableCell>().Count()),
                    Has.All.EqualTo(3),
                    "All rows should have the same number of cells");
                Assert.That(rows[0].GetFirstChild<TableCell>()?.TableCellProperties?
                    .VerticalMerge?.Val?.Value, Is.EqualTo(MergedCellValues.Restart));
                Assert.That(rows[1].GetFirstChild<TableCell>()?.TableCellProperties?
                    .VerticalMerge?.Val?.Value, Is.EqualTo(MergedCellValues.Continue));
                Assert.That(rows[2].GetFirstChild<TableCell>()?.TableCellProperties?
                    .VerticalMerge?.Val?.Value, Is.EqualTo(MergedCellValues.Continue));
                Assert.That(rows[3].GetFirstChild<TableCell>()?.TableCellProperties?
                    .VerticalMerge?.Val?.Value, Is.Null,
                    "Row on tfoot should not continue the span");
            });
        }

        [Test]
        public void ParseRowSpan()
        {
            var elements = converter.Parse(@"<table>
                    <tr><td>Cell 1.1</td><td>Cell 1.2</td><td>Cell 1.3</td></tr>
                    <tr><td>Cell 2.1</td><td rowspan=""2"">Cell 2.2</td><td>Cell 2.3</td></tr>
                    <tr><td>Cell 3.1</td><td>Cell 3.3</td></tr>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var rows = elements[0].Elements<TableRow>();
            Assert.That(rows.Count(), Is.EqualTo(3));
            Assert.That(rows.Select(r => r.Elements<TableCell>().Count()), 
                Has.All.EqualTo(3),
                "All rows should have the same number of cells");
            
            Assert.That(rows.ElementAt(1).Elements<TableCell>().ElementAt(1)?.TableCellProperties?.VerticalMerge?.Val?.Value, Is.EqualTo(MergedCellValues.Restart));
            Assert.That(rows.ElementAt(2).Elements<TableCell>().ElementAt(1)?.TableCellProperties?.VerticalMerge?.Val?.Value, Is.EqualTo(MergedCellValues.Continue));
        }

        [Test]
        public void ParseRowAndColumnSpan()
        {
            var elements = converter.Parse(@"<table>
                    <tr><td rowspan=""2"" colspan=""2"">Cell 1.1</td><td>Cell 1.3</td></tr>
                    <tr><td>Cell 2.3</td></tr>
                    <tr><td>Cell 3.1</td><td>Cell 3.2</td><td>Cell 3.3</td></tr>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var rows = elements[0].Elements<TableRow>();
            Assert.That(rows.Count(), Is.EqualTo(3));
            Assert.That(rows.Take(2).Select(r => r.Elements<TableCell>().Count()), 
                Has.All.EqualTo(2),
                "1st and 2nd rows should have 2 cells");
            Assert.That(rows.Last().Elements<TableCell>().Count(), 
                Is.EqualTo(3),
                "3rd row should have 3 cells");
            Assert.That(rows.First().GetFirstChild<TableCell>()?.TableCellProperties?.GridSpan?.Val?.Value, Is.EqualTo(2));
            Assert.That(rows.First().GetFirstChild<TableCell>()?.TableCellProperties?.VerticalMerge?.Val?.Value, Is.EqualTo(MergedCellValues.Restart));

            Assert.That(rows.ElementAt(1).GetFirstChild<TableCell>()?.TableCellProperties?.GridSpan?.Val?.Value, Is.EqualTo(2));
            Assert.That(rows.ElementAt(1).GetFirstChild<TableCell>()?.TableCellProperties?.VerticalMerge?.Val?.Value, Is.EqualTo(MergedCellValues.Continue));
        }

        [TestCase("tb-lr", "btLr")]
        [TestCase("vertical-lr", "btLr")]
        [TestCase("tb-rl", "tbRl")]
        [TestCase("vertical-rl", "tbRl")]
        public void ParseVerticalText(string direction, string openXmlDirection)
        {
            var elements = converter.Parse(@$"<table>
                    <tr><td style=""writing-mode:{direction}"">Cell 1.1</td></tr>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var rows = elements[0].Elements<TableRow>();
            Assert.That(rows.Count(), Is.EqualTo(1));
            Assert.That(rows.First().Elements<TableCell>().Count(), Is.EqualTo(1));
            var cell = rows.First().GetFirstChild<TableCell>();
            Assert.That(cell?.TableCellProperties?.TextDirection?.Val?.Value, Is.EqualTo(new TextDirectionValues(openXmlDirection)));
            Assert.That(cell?.TableCellProperties?.TableCellVerticalAlignment?.Val?.Value, Is.EqualTo(TableVerticalAlignmentValues.Center));
        }

        [TestCase("above", 0, 1)]
        [TestCase("below", 1, 0)]
        public void ParseTableCaption(string position, int captionPos, int tablePos)
        {
            converter.TableCaptionPosition = new (position);
            var elements = converter.Parse(@$"<table>
                    <caption>Some table caption</caption>
                    <tr><td>Cell 1.1</td></tr>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(2));
            Assert.That(elements[captionPos], Is.TypeOf<Paragraph>());
            Assert.That(elements[tablePos], Is.TypeOf<Table>());
            var p = (Paragraph) elements[captionPos];
            var runs = p.Elements<Run>();
            Assert.That(runs.Count(), Is.AtLeast(4));

            Assert.Multiple(() =>{
                Assert.That(p.ParagraphProperties.ParagraphStyleId?.Val?.Value, Is.EqualTo(converter.HtmlStyles.DefaultStyles.CaptionStyle));
                Assert.That(runs.First().HasChild<FieldChar>(), Is.True);
                Assert.That(runs.ElementAt(1).HasChild<FieldCode>(), Is.True);
                Assert.That(runs.ElementAt(2).HasChild<FieldChar>(), Is.True);
            });
            Assert.Multiple(() =>
            {
                Assert.That(runs.First().GetFirstChild<FieldChar>().FieldCharType.Value, Is.EqualTo(FieldCharValues.Begin));
                Assert.That(runs.ElementAt(1).GetFirstChild<FieldCode>().InnerText, Is.EqualTo("SEQ TABLE \\* ARABIC"));
                Assert.That(runs.ElementAt(2).GetFirstChild<FieldChar>().FieldCharType.Value, Is.EqualTo(FieldCharValues.End));
                Assert.That(runs.Last().InnerText, Is.EqualTo("Some table caption"));
            });
        }

        [Test]
        public void IgnoreEmptyTableCaption()
        {
            var elements = converter.Parse(@$"<table>
                    <caption></caption>
                    <tr><td>Cell 1.1</td></tr>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0], Is.TypeOf<Table>());
        }

        [Test]
        public void ParsePreAsTable()
        {
            const string preformattedText = @"
              ^__^
              (oo)\_______
              (__)\       )\/\
                  ||----w |
                  ||     ||";

            converter.RenderPreAsTable = true;
            var elements = converter.Parse(@$"
<pre role='img' aria-label='ASCII COW'>
{preformattedText}</pre>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var tableProps = elements[0].GetFirstChild<TableProperties>();
            Assert.That(tableProps, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tableProps.GetFirstChild<TableStyle>()?.Val?.Value, Is.EqualTo(converter.HtmlStyles.DefaultStyles.PreTableStyle));
                Assert.That(tableProps.GetFirstChild<TableWidth>()?.Type?.Value, Is.EqualTo(TableWidthUnitValues.Auto));
                Assert.That(tableProps.GetFirstChild<TableWidth>()?.Width?.Value, Is.EqualTo("0"));
            });

            var rows = elements[0].Elements<TableRow>();
            Assert.That(rows.Count(), Is.EqualTo(1));
            var cells = rows.First().Elements<TableCell>();
            Assert.That(cells.Count(), Is.EqualTo(1));
            var cell = cells.First();
            Assert.Multiple(() =>
            {
                Assert.That(cell.InnerText, Is.EqualTo(preformattedText));
                Assert.That(cell.TableCellProperties?.TableCellBorders.ChildElements.Count(), Is.EqualTo(4));
                Assert.That(cell.TableCellProperties?.TableCellBorders.ChildElements, Has.All.InstanceOf<BorderType>());
                Assert.That(cell.TableCellProperties?.TableCellBorders.Elements<BorderType>().All(b => b.Val.Value == BorderValues.Single), Is.True);
            });
        }

        [Test]
        public void ParseRowStyle()
        {
            var elements = converter.Parse(@$"<table>
                    <tr style='background-color:silver;'><td>Cell</td></tr>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var cell = elements[0].GetFirstChild<TableRow>()?.GetFirstChild<TableCell>();
            Assert.That(cell, Is.Not.Null);
            Assert.That(cell.TableCellProperties, Is.Not.Null);
            Assert.That(cell.TableCellProperties.Shading?.Fill?.Value, Is.EqualTo("C0C0C0"));

            var runProperties = cell.GetFirstChild<Paragraph>()?.GetFirstChild<Run>()?.RunProperties;
            Assert.That(runProperties?.Shading, Is.Null);
        }

        [Test]
        public void ParseCellStyle()
        {
            var elements = converter.Parse(@$"<table>
                    <tr><td style=""font-weight:bold""><i>Cell</i></td></tr>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var cell = elements[0].GetFirstChild<TableRow>()?.GetFirstChild<TableCell>();
            Assert.That(cell, Is.Not.Null);
            var runProperties = cell.GetFirstChild<Paragraph>()?.GetFirstChild<Run>()?.RunProperties;
            Assert.That(runProperties, Is.Not.Null);
            Assert.Multiple(() => {
                Assert.That(runProperties.Bold, Is.Not.Null);
                Assert.That(runProperties.Italic, Is.Not.Null);
            });
            Assert.Multiple(() => {
                // normally, Val should be null
                if (runProperties.Bold.Val is not null)
                    Assert.That(runProperties.Bold.Val, Is.EqualTo(true));
                if (runProperties.Italic.Val is not null)
                    Assert.That(runProperties.Italic.Val, Is.EqualTo(true));
            });
        }

        [Test]
        public void ParseNestedTable()
        {
            var elements = converter.Parse(@$"<table>
                    <tr><td style=""font-weight:bold"">
                        <table><tr><td>Cell</td></tr></table>
                    </td></tr>
                </table>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            Assert.That(elements[0].GetFirstChild<TableGrid>().Elements<GridColumn>().Count(), Is.EqualTo(1));
            var cell = elements[0].GetFirstChild<TableRow>()?.GetFirstChild<TableCell>();
            Assert.That(cell, Is.Not.Null);
            Assert.That(cell.HasChild<Table>(), Is.True);
        }

        [Test]
        public void ParseCol()
        {
            var elements = converter.Parse(@$"<table>
                    <colgroup>
                        <col style=""width:100px""/>
                        <col style=""width:50px""/>
                    </colgroup>
                    <tr><td>Cell 1.1</td><td>Cell 1.2</td></tr>
                </table>");
            
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var columns = elements[0].GetFirstChild<TableGrid>()?.Elements<GridColumn>();
            Assert.That(columns, Is.Not.Null);
            Assert.That(columns.Count(), Is.EqualTo(2));
            Assert.That(columns.First().Width?.Value, Is.EqualTo("1500"));
            Assert.That(columns.Last().Width?.Value, Is.EqualTo("750"));
        }

        [Test]
        public void ParseColWithSpan()
        {
            var elements = converter.Parse(@$"<table>
                    <colgroup>
                        <col style=""width:100px"" span=""2"" />
                        <col style=""width:50px""/>
                    </colgroup>
                    <tr><td>Cell 1.1</td><td>Cell 1.2</td><td>Cell 1.3</td></tr>
                </table>");
            
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements, Has.All.TypeOf<Table>());
            var columns = elements[0].GetFirstChild<TableGrid>()?.Elements<GridColumn>();
            Assert.That(columns, Is.Not.Null);
            Assert.That(columns.Count(), Is.EqualTo(3));
            Assert.That(columns.First().Width?.Value, Is.EqualTo("1500"));
            Assert.That(columns.ElementAt(1).Width?.Value, Is.EqualTo("1500"));
            Assert.That(columns.Last().Width?.Value, Is.EqualTo("750"));
        }
    }
}