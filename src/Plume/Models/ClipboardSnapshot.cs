using System.Collections.Specialized;
using System.IO;
using System.Windows.Media.Imaging;

namespace Plume.Models;

/// <summary>Instantané du presse-papiers (texte, image ou liste de fichiers), pour le sauvegarder avant un
/// collage/une capture de sélection puis le restaurer ensuite. Formats exotiques (objets Office, rendu
/// différé) hors périmètre : ils ne sont ni sauvegardés ni restaurés (comportement inchangé pour eux).</summary>
public sealed class ClipboardSnapshot
{
    private enum Kind { Empty, Text, Image, Files }

    private readonly Kind _kind;
    private readonly string? _text;
    private readonly BitmapSource? _image;
    private readonly StringCollection? _files;

    private ClipboardSnapshot(Kind kind, string? text, BitmapSource? image, StringCollection? files)
    {
        _kind = kind;
        _text = text;
        _image = image;
        _files = files;
    }

    private static readonly ClipboardSnapshot EmptyInstance = new(Kind.Empty, null, null, null);

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

    /// <summary>Capture l'état courant du presse-papiers (texte, image ou fichiers, dans cet ordre de priorité
    /// s'il y en a plusieurs), avec reprises si le presse-papiers est momentanément verrouillé. Ne lève jamais.</summary>
    public static ClipboardSnapshot Capture()
    {
        ClipboardSnapshot? snap = null;
        try
        {
            WithRetry(() =>
            {
                if (Clipboard.ContainsFileDropList())
                    snap = new ClipboardSnapshot(Kind.Files, null, null, Clipboard.GetFileDropList());
                else if (Clipboard.ContainsImage())
                    snap = new ClipboardSnapshot(Kind.Image, null, Clipboard.GetImage(), null);
                else if (Clipboard.ContainsText())
                    snap = new ClipboardSnapshot(Kind.Text, Clipboard.GetText(), null, null);
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
                    case Kind.Text: Clipboard.SetText(_text ?? ""); break;
                    case Kind.Image: Clipboard.SetImage(_image!); break;
                    case Kind.Files: Clipboard.SetFileDropList(_files!); break;
                    default: Clipboard.Clear(); break;
                }
            });
        }
        catch { }
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
