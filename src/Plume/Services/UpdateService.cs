using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Plume.Services;

/// <summary>Dernière version publiée : numéro, étiquette git, URL API de l'archive et empreinte attendue.</summary>
public sealed record UpdateInfo(Version Version, string Tag, string AssetApiUrl, string? Sha256);

/// <summary>Erreur de mise à jour, porteuse d'un message français affichable tel quel.</summary>
public sealed class UpdateException : Exception
{
    public UpdateException(string message) : base(message) { }
}

/// <summary>
/// Mise à jour à la demande (bouton « Vérifier les mises à jour » de l'onglet À propos) : lit la dernière
/// Release du dépôt GitHub, télécharge l'archive, vérifie son empreinte, remplace l'exe et ses ressources.
/// RIEN n'est fait au démarrage ni en arrière-plan : aucun appel réseau sans clic (RÈGLE PRIMORDIALE et
/// docs/confidentialite-flux-donnees.md). Le dépôt étant privé, l'accès passe par un jeton : celui des
/// Réglages, sinon la connexion Git déjà enregistrée sur le poste (Git Credential Manager).
/// </summary>
public static partial class UpdateService
{
    /// <summary>Dépôt GitHub qui porte les Releases (propriétaire/nom).</summary>
    public const string Repository = "alexandreb06800/Plume";

    private const string ExeName = "Plume.exe";
    private const string OldExeName = "Plume.old.exe";

    // ---------- Logique pure (testée) ----------

    /// <summary>« v5.4.8 » ou « 5.4.8 » → 5.4.8 ; null si l'étiquette n'est pas une version.</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var t = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(t, out var v) && v.Build >= 0 ? new Version(v.Major, v.Minor, v.Build) : null;
    }

    /// <summary>Vrai si <paramref name="latest"/> est strictement plus récente (majeur.mineur.correctif).</summary>
    public static bool IsNewer(Version current, Version latest)
        => new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build))
           > new Version(current.Major, current.Minor, Math.Max(0, current.Build));

    /// <summary>Empreinte SHA-256 annoncée dans le texte de la Release (« sha256: … », 64 caractères hexadécimaux).</summary>
    public static string? ExtractSha256(string? body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        var m = Sha256Pattern().Match(body);
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
    }

    [GeneratedRegex(@"sha256\s*:\s*([0-9a-fA-F]{64})", RegexOptions.IgnoreCase)]
    private static partial Regex Sha256Pattern();

    /// <summary>Lit la réponse de l'API « releases/latest ». Null si elle ne décrit pas une version exploitable
    /// (étiquette illisible, ou aucune archive « Plume-….zip » jointe).</summary>
    public static UpdateInfo? ParseLatestRelease(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var version = ParseTag(tag);
            if (version is null || tag is null) return null;
            var body = root.TryGetProperty("body", out var b) ? b.GetString() : null;
            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                var url = a.TryGetProperty("url", out var u) ? u.GetString() : null;
                if (name is null || url is null) continue;
                if (name.StartsWith("Plume-", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return new UpdateInfo(version, tag, url, ExtractSha256(body));
            }
            return null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Empreinte SHA-256 d'un fichier, en hexadécimal minuscule.</summary>
    public static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>Installe l'archive dans <paramref name="installDir"/> : extraction dans un dossier temporaire, puis
    /// l'exe en cours est RENOMMÉ (Windows l'autorise pour un exe en cours d'exécution, pas son écrasement) et les
    /// nouveaux fichiers prennent sa place. En cas d'échec, l'ancien exe est remis : jamais d'installation à moitié
    /// faite. L'ancien exe renommé est supprimé au prochain démarrage (<see cref="CleanupOld"/>).</summary>
    public static void ApplyArchive(string zipPath, string installDir)
    {
        var staging = Path.Combine(installDir, "update-staging");
        try
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            ZipFile.ExtractToDirectory(zipPath, staging);
            if (!File.Exists(Path.Combine(staging, ExeName)))
                throw new UpdateException("L'archive de mise à jour ne contient pas Plume.exe.");

            var exe = Path.Combine(installDir, ExeName);
            var old = Path.Combine(installDir, OldExeName);
            if (File.Exists(old)) File.Delete(old);
            bool renamed = false;
            if (File.Exists(exe)) { File.Move(exe, old); renamed = true; }
            try
            {
                CopyTree(staging, installDir);
            }
            catch
            {
                // Retour arrière : l'ancien exe reprend sa place.
                try
                {
                    if (File.Exists(exe)) File.Delete(exe);
                    if (renamed) File.Move(old, exe);
                }
                catch { /* au pire l'ancien exe reste sous son nom de secours, rien n'est perdu */ }
                throw;
            }
        }
        catch (UpdateException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new UpdateException("Impossible d'installer la mise à jour (fichier verrouillé ou archive illisible).");
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
        }
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(from))
            CopyTree(dir, Path.Combine(to, Path.GetFileName(dir)));
    }

    /// <summary>Supprime l'exe de la version précédente laissé par une mise à jour. Ne lève jamais.</summary>
    public static void CleanupOld(string installDir)
    {
        try
        {
            var old = Path.Combine(installDir, OldExeName);
            if (File.Exists(old)) File.Delete(old);
        }
        catch { /* encore verrouillé : on réessaiera au prochain démarrage */ }
    }

    // ---------- Réseau ----------

    private static HttpClient CreateClient(string token)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Plume", "1.0")); // exigé par l'API GitHub
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return http;
    }

    /// <summary>Dernière version publiée, ou null s'il n'existe encore aucune Release exploitable.</summary>
    public static async Task<UpdateInfo?> GetLatestAsync(string token, CancellationToken ct = default)
    {
        using var http = CreateClient(token);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        HttpResponseMessage resp;
        try { resp = await http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new UpdateException("Pas de réseau : impossible de joindre GitHub.");
        }
        using var _ = resp;
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new UpdateException("Jeton GitHub refusé (expiré ou invalide).");
        if (resp.StatusCode == HttpStatusCode.NotFound)
            // Dépôt privé : GitHub répond 404 aussi bien « aucune Release » que « jeton sans accès au dépôt ».
            throw new UpdateException("Aucune version publiée, ou le jeton n'a pas accès au dépôt.");
        if (!resp.IsSuccessStatusCode)
            throw new UpdateException($"GitHub a répondu une erreur ({(int)resp.StatusCode}).");
        return ParseLatestRelease(await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Télécharge l'archive de la version et vérifie son empreinte. Renvoie le chemin du fichier.</summary>
    public static async Task<string> DownloadAsync(string token, UpdateInfo info, string destDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destDir);
        var path = Path.Combine(destDir, $"Plume-{info.Version}.zip");
        using var http = CreateClient(token);
        // « application/octet-stream » : l'API renvoie le fichier (via une redirection signée, sans le jeton).
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        try
        {
            using var resp = await http.GetAsync(info.AssetApiUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
                throw new UpdateException($"Téléchargement refusé par GitHub ({(int)resp.StatusCode}).");
            await using var file = File.Create(path);
            await resp.Content.CopyToAsync(file, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new UpdateException("Le téléchargement de la mise à jour a échoué.");
        }

        // Empreinte obligatoire : un exe non signé ne s'installe pas sans contrôle d'intégrité.
        if (info.Sha256 is null)
            throw new UpdateException("Cette version ne publie pas d'empreinte de contrôle : installation refusée.");
        if (!string.Equals(Sha256Of(path), info.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(path); } catch { }
            throw new UpdateException("L'archive téléchargée ne correspond pas à l'empreinte publiée : installation refusée.");
        }
        return path;
    }

    // ---------- Jeton ----------

    /// <summary>Jeton à utiliser : celui des Réglages, sinon la connexion Git enregistrée sur le poste
    /// (« git credential fill », sans jamais ouvrir de fenêtre). Null si aucun n'est disponible.</summary>
    public static string? ResolveToken(string? configured)
        => !string.IsNullOrWhiteSpace(configured) ? configured.Trim() : TokenFromGit();

    private static string? TokenFromGit()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "credential fill")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // Aucune invite : si Git n'a pas déjà la connexion, on renonce en silence.
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            psi.Environment["GCM_INTERACTIVE"] = "never";
            using var p = Process.Start(psi);
            if (p is null) return null;
            p.StandardInput.Write("protocol=https\nhost=github.com\n\n");
            p.StandardInput.Close();
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return null; }
            foreach (var line in output.Split('\n'))
                if (line.StartsWith("password=", StringComparison.Ordinal))
                {
                    var token = line["password=".Length..].Trim();
                    return token.Length > 0 ? token : null;
                }
            return null;
        }
        catch { return null; } // Git absent du poste : pas de jeton par ce moyen
    }
}
