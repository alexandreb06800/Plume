using System.Collections.Specialized;
using System.IO;
using System.Windows.Media.Imaging;

namespace Plume.Models;

/// <summary>Instantané du presse-papiers, pour le sauvegarder avant un collage/une capture de sélection
/// puis le restaurer ensuite. Conserve TOUS les formats utiles (texte, HTML, RTF, cellules Excel, fichiers +
/// effet copier/couper, image…) : copier des cellules Excel puis dicter ne doit pas les transformer en
/// image, ni copier depuis Word faire perdre la mise en forme. Repli sur l'ancien instantané à format
/// unique si la capture multi-formats échoue ou dépasse la taille maximale.</summary>
public sealed class ClipboardSnapshot
{
    private enum Kind { Empty, Formats, Text, Image, Files }

    /// <summary>Plafond de l'instantané multi-formats (au-delà : repli à format unique, borné en mémoire).</summary>
    private const long MaxTotalBytes = 64L * 1024 * 1024;

    private readonly Kind _kind;
    private readonly string? _text;
    private readonly BitmapSource? _image;
    private readonly StringCollection? _files;
    private readonly List<KeyValuePair<string, object>>? _formats; // format → string / string[] / byte[] / BitmapSource

    private ClipboardSnapshot(Kind kind, string? text = null, BitmapSource? image = null,
                              StringCollection? files = null, List<KeyValuePair<string, object>>? formats = null)
    {
        _kind = kind;
        _text = text;
        _image = image;
        _files = files;
        _formats = formats;
    }

    private static readonly ClipboardSnapshot EmptyInstance = new(Kind.Empty);

    /// <summary>Presse-papiers vide (rien à restaurer) : sert de valeur par défaut sûre. Ne lève jamais.</summary>
    public static ClipboardSnapshot Empty() => EmptyInstance;

    /// <summary>Exécute une opération presse-papiers avec 2 reprises rapprochées (60 ms) : le presse-papiers
    /// peut être tenu un instant par un autre processus (OneDrive, Office…) — même motif que le bouton
    /// Copier d'AnswerWindow. Synchrone et borné (~120 ms au pire), UNIQUEMENT sur échec ; le chemin
    /// nominal ne paie rien. Relaie la dernière exception si tout échoue.</summary>
    public static void WithRetry(Action op)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { op(); return; }
            catch when (attempt < 2)
            {
                System.Threading.Thread.Sleep(60);
            }
        }
    }

    // Formats à handle GDI (non transportables tels quels — Windows les resynthétise depuis le DIB) et
    // formats OLE liés à l'objet source vivant (stockage OLE, liens) : jamais copiés.
    private static readonly HashSet<string> SkippedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bitmap", "EnhancedMetafile", "MetaFilePict", "Palette",
        "Embed Source", "Link Source", "Link Source Descriptor", "Object Descriptor",
        "Native", "OwnerLink", "ObjectLink", "Ole Private Data", "DataObject",
    };

    // Images bitmap transportables en flux (DIB, DIBV5).
    private static readonly HashSet<string> DibFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "DeviceIndependentBitmap", "Format17",
    };

    // Formats de document Office : leur présence signifie que l'image proposée n'est qu'un aperçu rendu à
    // la demande (cellules Excel, texte Word…) — la demander forcerait un rendu coûteux pour rien.
    private static readonly HashSet<string> OfficeDocumentFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "Biff12", "Biff8", "Biff5", "XML Spreadsheet", "Rich Text Format", "Embed Source",
    };

    // Cellules Excel : Biff12 (format natif) + Biff8 + HTML suffisent à recoller. Les variantes ci-dessous
    // sont rendues à la demande par Excel et coûtent cher à froid (RTF ~200 ms mesurés) sur le chemin critique.
    private static readonly HashSet<string> RedundantWithBiff12 = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rich Text Format", "XML Spreadsheet", "Biff5",
    };

    /// <summary>Formats à lire pour l'instantané, dans l'ordre d'origine. Exclut les formats GDI/OLE, en
    /// présence d'un document Office l'image d'aperçu, et en présence de cellules Excel (Biff12) les
    /// variantes redondantes coûteuses. Pur et testable.</summary>
    public static List<string> PlanFormats(IEnumerable<string> available)
    {
        var list = available.Where(f => !string.IsNullOrEmpty(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        bool officeDocument = list.Any(OfficeDocumentFormats.Contains);
        bool excelCells = list.Contains("Biff12", StringComparer.OrdinalIgnoreCase);
        return list
            .Where(f => !SkippedFormats.Contains(f))
            .Where(f => !(officeDocument && DibFormats.Contains(f)))
            .Where(f => !(excelCells && RedundantWithBiff12.Contains(f)))
            .ToList();
    }

    /// <summary>Capture l'état courant du presse-papiers (tous les formats utiles), avec reprises si le
    /// presse-papiers est momentanément verrouillé. Ne lève jamais.</summary>
    public static ClipboardSnapshot Capture()
    {
        try
        {
            var multi = CaptureFormats();
            if (multi is not null) return multi;
        }
        catch { /* repli ci-dessous */ }
        return CaptureSingle();
    }

    /// <summary>Instantané multi-formats ; null si rien d'exploitable ou trop volumineux (→ repli).</summary>
    private static ClipboardSnapshot? CaptureFormats()
    {
        System.Windows.IDataObject? data = null;
        string[] available = Array.Empty<string>();
        WithRetry(() =>
        {
            data = Clipboard.GetDataObject();
            available = data?.GetFormats(false) ?? Array.Empty<string>();
        });
        if (data is null || available.Length == 0) return null;

        var formats = new List<KeyValuePair<string, object>>();
        long total = 0;
        foreach (var format in PlanFormats(available))
        {
            object? value;
            try { value = data.GetData(format, false); }
            catch { continue; } // format annoncé mais indisponible : on l'ignore
            switch (value)
            {
                case string s:
                    total += s.Length * 2L;
                    formats.Add(new(format, s));
                    break;
                case string[] arr:
                    formats.Add(new(format, arr));
                    break;
                case MemoryStream ms:
                    var bytes = ms.ToArray();
                    total += bytes.Length;
                    formats.Add(new(format, bytes));
                    break;
                case BitmapSource bmp:
                    if (!bmp.IsFrozen && bmp.CanFreeze) bmp.Freeze();
                    total += (long)bmp.PixelWidth * bmp.PixelHeight * 4;
                    formats.Add(new(format, bmp));
                    break;
                // Autres types (objets sérialisés .NET, flux COM…) : non restaurables sans risque, ignorés.
            }
            if (total > MaxTotalBytes) return null;
        }
        return formats.Count == 0 ? null : new ClipboardSnapshot(Kind.Formats, formats: formats);
    }

    /// <summary>Ancien instantané à format unique (fichiers > texte > image), conservé en repli.
    /// Le texte passe avant l'image : des cellules Excel annoncent aussi une image d'aperçu.</summary>
    private static ClipboardSnapshot CaptureSingle()
    {
        ClipboardSnapshot? snap = null;
        try
        {
            WithRetry(() =>
            {
                if (Clipboard.ContainsFileDropList())
                    snap = new ClipboardSnapshot(Kind.Files, files: Clipboard.GetFileDropList());
                else if (Clipboard.ContainsText())
                    snap = new ClipboardSnapshot(Kind.Text, text: Clipboard.GetText());
                else if (Clipboard.ContainsImage())
                    snap = new ClipboardSnapshot(Kind.Image, image: Clipboard.GetImage());
            });
        }
        catch { }
        return snap ?? EmptyInstance;
    }

    /// <summary>Restaure ce contenu dans le presse-papiers (le vide si rien n'avait été capturé), avec
    /// reprises : perdre le presse-papiers ORIGINAL de l'utilisateur sur un verrou transitoire serait
    /// définitif. Ne lève jamais.</summary>
    public void Restore()
    {
        try
        {
            WithRetry(() =>
            {
                switch (_kind)
                {
                    case Kind.Formats: Clipboard.SetDataObject(BuildDataObject(), true); break;
                    case Kind.Text: Clipboard.SetText(_text ?? ""); break;
                    case Kind.Image: Clipboard.SetImage(_image!); break;
                    case Kind.Files: Clipboard.SetFileDropList(_files!); break;
                    default: Clipboard.Clear(); break;
                }
            });
        }
        catch
        {
            // Restauration multi-formats refusée (format inattendu) : au moins le texte, s'il existe.
            if (_kind == Kind.Formats && FirstText() is string t)
                try { WithRetry(() => Clipboard.SetText(t)); } catch { }
        }
    }

    /// <summary>Nouveau DataObject à chaque appel : les flux sont recréés (une reprise relit depuis le début).</summary>
    private DataObject BuildDataObject()
    {
        var obj = new DataObject();
        foreach (var (format, value) in _formats!)
        {
            try { obj.SetData(format, value is byte[] b ? new MemoryStream(b) : value, false); }
            catch { /* format refusé par WPF : on restaure les autres */ }
        }
        return obj;
    }

    private string? FirstText()
    {
        foreach (var (format, value) in _formats ?? new())
            if (value is string s && (format == DataFormats.UnicodeText || format == DataFormats.Text)) return s;
        return null;
    }

    /// <summary>Écrit `text` dans le presse-papiers en le marquant comme transitoire : Windows ne doit ni le
    /// garder dans l'historique (Win+V) ni le synchroniser au presse-papiers infonuagique. Le texte dicté ne
    /// reste dans le presse-papiers que le temps du Ctrl+V (quelques centaines de ms) avant restauration —
    /// il n'a pas sa place dans un historique persistant. Si le marquage échoue, on retombe sur un SetText
    /// classique : le collage prime toujours sur la confidentialité de l'historique. Ne lève que si les deux
    /// tentatives échouent (presse-papiers verrouillé), comme <see cref="Clipboard.SetText"/>.</summary>
    public static void SetTextExcludedFromHistory(string text)
    {
        try
        {
            WithRetry(() =>
            {
                var data = new DataObject();
                data.SetText(text);
                // Formats reconnus par le moniteur d'historique/synchro de Windows (mêmes que ceux utilisés par
                // Chrome en navigation privée) : la seule présence du premier suffit, les deux autres sont une
                // ceinture-et-bretelles pour les versions de Windows qui ne reconnaîtraient que l'un des trois.
                data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[] { 1 }));
                data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
                data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
                Clipboard.SetDataObject(data, true);
            });
        }
        catch
        {
            WithRetry(() => Clipboard.SetText(text));
        }
    }
}
