using Plume.Models;
using Xunit;

namespace Plume.Tests;

public class ClipboardSnapshotTests
{
    [Fact]
    public void Excel_GardeLesCellulesEtIgnoreLImageDApercu()
    {
        var formats = new[]
        {
            "EnhancedMetafile", "MetaFilePict", "Bitmap", "Biff12", "Biff8", "HTML Format", "Rich Text Format",
            "Csv", "UnicodeText", "Text", "Locale", "DeviceIndependentBitmap", "Format17", "Embed Source",
            "Object Descriptor", "Link Source", "Link Source Descriptor", "Ole Private Data",
        };
        var plan = ClipboardSnapshot.PlanFormats(formats);
        // RTF écarté : redondant avec Biff12 + HTML, et ~200 ms de rendu à froid côté Excel.
        Assert.Equal(new[] { "Biff12", "Biff8", "HTML Format", "Csv", "UnicodeText", "Text", "Locale" }, plan);
    }

    [Fact]
    public void Word_GardeLeRtf()
    {
        // Sans cellules Excel, le RTF est le format riche principal (Word, WordPad…).
        var plan = ClipboardSnapshot.PlanFormats(new[] { "Rich Text Format", "HTML Format", "UnicodeText", "EnhancedMetafile", "Embed Source" });
        Assert.Equal(new[] { "Rich Text Format", "HTML Format", "UnicodeText" }, plan);
    }

    [Fact]
    public void CaptureDEcran_GardeLeDib()
    {
        var plan = ClipboardSnapshot.PlanFormats(new[] { "Bitmap", "DeviceIndependentBitmap", "Format17" });
        Assert.Equal(new[] { "DeviceIndependentBitmap", "Format17" }, plan);
    }

    [Fact]
    public void ImageCopieeDuNavigateur_GardeHtmlEtImage()
    {
        // Pas de document Office : l'image est le contenu, pas un aperçu.
        var plan = ClipboardSnapshot.PlanFormats(new[] { "HTML Format", "Bitmap", "DeviceIndependentBitmap", "PNG" });
        Assert.Equal(new[] { "HTML Format", "DeviceIndependentBitmap", "PNG" }, plan);
    }

    [Fact]
    public void Fichiers_GardeLaListeEtLEffetCopierCouper()
    {
        var plan = ClipboardSnapshot.PlanFormats(new[] { "Shell IDList Array", "FileDrop", "FileNameW", "Preferred DropEffect" });
        Assert.Equal(new[] { "Shell IDList Array", "FileDrop", "FileNameW", "Preferred DropEffect" }, plan);
    }

    [Fact]
    public void FormatsVidesOuEnDouble_Ignores()
    {
        var plan = ClipboardSnapshot.PlanFormats(new[] { "", "UnicodeText", "unicodetext" });
        Assert.Single(plan);
    }
}
