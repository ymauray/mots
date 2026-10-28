using System.IO;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.CustomXmlDataProperties;
using DocumentFormat.OpenXml.Packaging;

namespace Mots;

/// <summary>Le fichier ne peut pas être ouvert par Mots (pas créé par Mots, ou version trop récente).</summary>
public sealed class IncompatibleFileException(string message) : Exception(message);

/// <summary>
/// Marqueur de compatibilité stocké dans une partie « Custom XML » du .docx : Word ne l'affiche pas
/// et la conserve quand il réenregistre le document.
/// </summary>
internal static class MotsMetadata
{
    /// <summary>À incrémenter quand le contenu des fichiers change de façon incompatible avec les versions précédentes.</summary>
    public const int CurrentFormatVersion = 1;

    private static readonly XNamespace Ns = "urn:mots:metadata";

    public static void Write(MainDocumentPart main)
    {
        var xml = new XDocument(new XElement(Ns + "mots",
            new XAttribute("application", "Mots"),
            new XAttribute("formatVersion", CurrentFormatVersion)));

        var part = main.AddCustomXmlPart(CustomXmlPartType.CustomXml);
        using (var stream = new MemoryStream())
        {
            xml.Save(stream);
            stream.Position = 0;
            part.FeedData(stream);
        }

        // Word attend un identifiant associé à chaque partie Custom XML.
        var properties = part.AddNewPart<CustomXmlPropertiesPart>();
        properties.DataStoreItem = new DataStoreItem(
            new SchemaReferences(new SchemaReference { Uri = Ns.NamespaceName }))
        {
            ItemId = $"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}",
        };
    }

    /// <exception cref="IncompatibleFileException">Le marqueur est absent, illisible ou d'une version plus récente.</exception>
    public static void Verify(MainDocumentPart main)
    {
        foreach (var part in main.CustomXmlParts)
        {
            XElement? root;
            try
            {
                using var stream = part.GetStream();
                root = XDocument.Load(stream).Root;
            }
            catch (XmlException)
            {
                continue;
            }

            if (root?.Name != Ns + "mots") continue;

            if (!int.TryParse((string?)root.Attribute("formatVersion"), out var version) || version < 1)
                throw new IncompatibleFileException("Les informations de compatibilité Mots de ce fichier sont illisibles.");

            if (version > CurrentFormatVersion)
                throw new IncompatibleFileException(
                    "Ce document a été créé avec une version plus récente de Mots. Mettez Mots à jour pour l'ouvrir.");

            return;
        }

        throw new IncompatibleFileException(
            "Ce document n'a pas été créé avec Mots. Seuls les documents enregistrés par Mots peuvent être ouverts.");
    }
}
