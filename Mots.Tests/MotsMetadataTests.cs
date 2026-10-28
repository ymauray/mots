using DocumentFormat.OpenXml.Packaging;
using NUnit.Framework;
using static Mots.Tests.WordFile;

namespace Mots.Tests;

public class MotsMetadataTests : FileTestBase
{
    private static void Verify(string path)
    {
        using var package = WordprocessingDocument.Open(path, false);
        MotsMetadata.Verify(package.MainDocumentPart!);
    }

    [Test]
    public void Current_format_version_is_positive()
    {
        Assert.That(MotsMetadata.CurrentFormatVersion, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void Written_marker_verifies()
    {
        var path = PathOf("m.docx");
        DocxConverter.Save(new System.Windows.Documents.FlowDocument(), path);

        Assert.That(() => Verify(path), Throws.Nothing);
    }

    [Test]
    public void Verify_fails_without_any_custom_xml()
    {
        var path = Create(PathOf("m.docx"), [P(null, "x")]);

        Assert.That(() => Verify(path), Throws.InstanceOf<IncompatibleFileException>().With.Message.Contain("pas été créé avec Mots"));
    }

    [Test]
    public void Verify_finds_the_marker_among_several_custom_parts()
    {
        var path = Create(PathOf("m.docx"), [P(null, "x")], null, "<autre xmlns=\"urn:autre\"/>");
        using (var package = WordprocessingDocument.Open(path, true))
        {
            var part = package.MainDocumentPart!.AddCustomXmlPart(CustomXmlPartType.CustomXml);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Marker(MotsMetadata.CurrentFormatVersion)));
            part.FeedData(stream);
        }

        Assert.That(() => Verify(path), Throws.Nothing);
    }

    [Test]
    public void Verify_requires_the_mots_namespace()
    {
        var path = Create(PathOf("m.docx"), [P(null, "x")], null,
            "<mots xmlns=\"urn:autre\" application=\"Mots\" formatVersion=\"2\"/>");

        Assert.That(() => Verify(path), Throws.InstanceOf<IncompatibleFileException>());
    }

    [Test]
    public void Verify_accepts_the_current_version_exactly()
    {
        var path = Create(PathOf("m.docx"), [P(null, "x")], null, Marker(MotsMetadata.CurrentFormatVersion));

        Assert.That(() => Verify(path), Throws.Nothing);
    }

    [Test]
    public void Verify_rejects_the_next_version()
    {
        var path = Create(PathOf("m.docx"), [P(null, "x")], null, Marker(MotsMetadata.CurrentFormatVersion + 1));

        Assert.That(() => Verify(path), Throws.InstanceOf<IncompatibleFileException>().With.Message.Contain("Mettez Mots à jour"));
    }

    [Test]
    public void IncompatibleFileException_carries_its_message()
    {
        Assert.That(new IncompatibleFileException("boum").Message, Is.EqualTo("boum"));
    }
}
