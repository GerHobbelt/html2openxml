using NUnit.Framework;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HtmlToOpenXml.Tests
{
    /// <summary>
    /// Tests parser with various complex input Html.
    /// </summary>
    [TestFixture]
    public class ParserTests : HtmlConverterTestBase
    {
        [TestCase("<!--<p>some text</p>-->")]
        [TestCase("<script>document.getElementById('body');</script>")]
        [TestCase("<style>{font-size:2em}</script>")]
        [TestCase("<xml><element><childElement attr='value' /></element></xml>")]
        [TestCase("<button>Save</button>")]
        [TestCase("<input type='search' placeholder='Search' />")]

        public void ParseIgnore(string html)
        {
            // the inner html shouldn't be interpreted
            var elements = converter.Parse(html);
            Assert.That(elements, Is.Empty);
        }

        [Test]
        public void ParseUnclosedTag()
        {
            var elements = converter.Parse("<p>some text in <i>italics <b>,bold and italics</p>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0].ChildElements, Has.Count.EqualTo(3));

            var runProperties = elements[0].ChildElements[0].GetFirstChild<RunProperties>();
            Assert.That(runProperties, Is.Null);

            runProperties = elements[0].ChildElements[1].GetFirstChild<RunProperties>();
            Assert.That(runProperties, Is.Not.Null);
            Assert.That(runProperties.HasChild<Italic>(), Is.EqualTo(true));
            Assert.That(runProperties.HasChild<Bold>(), Is.EqualTo(false));

            runProperties = elements[0].ChildElements[2].GetFirstChild<RunProperties>();
            Assert.That(runProperties, Is.Not.Null);
            Assert.That(runProperties.HasChild<Italic>(), Is.EqualTo(true));
            Assert.That(runProperties.HasChild<Bold>(), Is.EqualTo(true));

            elements = converter.Parse("<p>First paragraph in semi-<i>italics <p>Second paragraph still italic <b>but also in bold</b></p>");
            Assert.That(elements, Has.Count.EqualTo(2));
            Assert.That(elements[0].ChildElements, Has.Count.EqualTo(2));
            Assert.That(elements[1].ChildElements, Has.Count.EqualTo(2));

            runProperties = elements[0].ChildElements[0].GetFirstChild<RunProperties>();
            Assert.That(runProperties, Is.Null);

            runProperties = elements[0].ChildElements[1].GetFirstChild<RunProperties>();
            Assert.That(runProperties, Is.Not.Null);
            Assert.That(runProperties.HasChild<Italic>(), Is.EqualTo(true));

            runProperties = elements[1].FirstChild.GetFirstChild<RunProperties>();
            Assert.That(runProperties, Is.Not.Null);
            Assert.That(runProperties.HasChild<Italic>(), Is.EqualTo(true));
            Assert.That(runProperties.HasChild<Bold>(), Is.EqualTo(false));

            runProperties = elements[1].ChildElements[1].GetFirstChild<RunProperties>();
            Assert.That(runProperties, Is.Not.Null);
            Assert.That(runProperties.HasChild<Italic>(), Is.EqualTo(true));
            Assert.That(runProperties.HasChild<Bold>(), Is.EqualTo(true));

            // this should generate a new paragraph with its own style
            elements = converter.Parse("<p>First paragraph in <i>italics </i><p>Second paragraph not in italic</p>");
            Assert.That(elements, Has.Count.EqualTo(2));
            Assert.That(elements[0].ChildElements, Has.Count.EqualTo(2));
            Assert.That(elements[1].ChildElements, Has.Count.EqualTo(1));
            Assert.That(elements[1].FirstChild, Is.TypeOf(typeof(Run)));

            runProperties = elements[1].FirstChild.GetFirstChild<RunProperties>();
            Assert.That(runProperties, Is.Null);
        }

        [TestCase("<p>Some\ntext</p>", ExpectedResult = 1)]
        [TestCase("<p>Some <b>bold\n</b>text</p>", ExpectedResult = 3)]
        [TestCase("\t<p>Some <b>bold\n</b>text</p>", ExpectedResult = 3)]
        [TestCase("  <p>Some text</p> ", ExpectedResult = 1)]
        public int ParseNewline (string html)
        {
            var elements = converter.Parse(html);
            return elements[0].Count(c => c is Run);
        }

        [Test]
        public void ParseDisorderedTable ()
        {
            // table parts should be reordered
            var elements = converter.Parse(@"
<table>
<tbody>
    <tr><td>Body</td></tr>
</tbody>
<thead>
    <tr><td>Header</td></tr>
</thead>
<tfoot>
    <tr><td>Footer</td></tr>
</tfoot>
</table>");

            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0], Is.TypeOf(typeof(Table)));

            var rows = elements[0].Elements<TableRow>();
            Assert.That(rows.Count(), Is.EqualTo(3));
            Assert.That(rows.ElementAt(0).InnerText, Is.EqualTo("Header"));
            Assert.That(rows.ElementAt(1).InnerText, Is.EqualTo("Body"));
            Assert.That(rows.ElementAt(2).InnerText, Is.EqualTo("Footer"));
        }

        [Test]
        public void ParseNotTag ()
        {
            var elements = converter.Parse(" < b >bold</b>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0].ChildElements, Has.Count.EqualTo(1));
            Assert.That(elements[0].FirstChild, Is.TypeOf<Run>());
            Assert.That(elements[0].FirstChild.InnerText, Is.EqualTo("< b >bold"));

            elements = converter.Parse(" <3");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0].ChildElements, Has.Count.EqualTo(1));
            Assert.That(elements[0].FirstChild, Is.TypeOf<Run>());
            Assert.That(elements[0].FirstChild.InnerText, Is.EqualTo("<3"));
        }

        [Test]
        public void ParseSpaceRuns ()
        {
            // the new line should generate a space between "bold" and "text"
            var elements = converter.Parse(" <span>This is a <b>bold\n</b>text</span>");
            Assert.That(elements, Has.Count.EqualTo(1));
            Assert.That(elements[0].ChildElements, Has.Count.EqualTo(3));
            Assert.That(elements[0].ChildElements.All(r => r is Run), Is.True);
            Assert.That(elements[0].InnerText, Is.EqualTo("This is a bold text"));
        }

        [Test]
        public void ParseParagraphCustomClass()
        {
            using var generatedDocument = new System.IO.MemoryStream();
            using (var buffer = ResourceHelper.GetStream("Resources.DocWithCustomStyle.docx"))
                buffer.CopyTo(generatedDocument);

            generatedDocument.Position = 0L;
            using WordprocessingDocument package = WordprocessingDocument.Open(generatedDocument, true);
            MainDocumentPart mainPart = package.MainDocumentPart;
            HtmlConverter converter = new HtmlConverter(mainPart);

            var elements = converter.Parse("<div class='CustomStyle1'>Lorem</div><span>Ipsum</span>");
            Assert.That(elements, Is.Not.Empty);
            var paragraphProperties = elements[0].GetFirstChild<ParagraphProperties>();
            Assert.That(paragraphProperties, Is.Not.Null);
            Assert.That(paragraphProperties.ParagraphStyleId, Is.Not.Null);
            Assert.That(paragraphProperties.ParagraphStyleId.Val.Value, Is.EqualTo("CustomStyle1"));
        }
    }
}