using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// PontDocuments.cs — les manuscrits mis en forme (.docx / .odt) : import
// en Markdown et export via Pandoc, extraction native du texte brut, et
// la chirurgie du .docx (correction directe de word/document.xml, styles
// conservés à l'identique).

namespace Typonanny
{
    // ------------------------------------------- documents (.docx / .odt)
    // Le pont vers les manuscrits mis en forme. Deux niveaux :
    // 1. Pandoc disponible (dossier de dépendances partagé de la famille
    //    Stargazer, téléchargé au premier lancement) : aller-retour complet
    //    — le document entre en Markdown éditable, ressort dans son format
    //    d'origine, mise en forme (gras, titres, listes…) conservée.
    // 2. Sans Pandoc : extraction native du texte brut depuis le XML
    //    (word/document.xml, content.xml) — lecture seule, sortie .txt/.md.

    public static class PontDocuments
    {
        // Le dossier de dépendances, PARTAGÉ par toutes les apps de la
        // famille Stargazer (un moteur n'est jamais téléchargé deux fois) :
        // STARGAZER_DEPS s'il est donné, sinon <hub>\dependencies quand l'app
        // vit dans Stargazer (Stargazer.exe deux niveaux au-dessus), sinon
        // %LOCALAPPDATA%\Stargazer\dependencies (app autonome).
        public static string DossierDependances(string appDir)
        {
            var env = Environment.GetEnvironmentVariable("STARGAZER_DEPS");
            if (!string.IsNullOrEmpty(env)) return env;
            try
            {
                var hub = Path.GetDirectoryName(Path.GetDirectoryName(appDir));
                if (hub != null && File.Exists(Path.Combine(hub, "Stargazer.exe")))
                    return Path.Combine(hub, "dependencies");
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.Combine("Stargazer", "dependencies"));
        }

        // Recherche : dossier de dépendances partagé → bin\ local (anciennes
        // installations autonomes) → PATH.
        public static string TrouverPandoc(string appDir)
        {
            try
            {
                var partage = Path.Combine(DossierDependances(appDir), "pandoc.exe");
                if (File.Exists(partage)) return partage;
            }
            catch { }
            var local = Path.Combine(Path.Combine(appDir, "bin"), "pandoc.exe");
            if (File.Exists(local)) return local;
            var chemins = Environment.GetEnvironmentVariable("PATH");
            if (chemins != null)
                foreach (var dir in chemins.Split(';'))
                {
                    if (dir.Trim().Length == 0) continue;
                    try
                    {
                        var full = Path.Combine(dir.Trim(), "pandoc.exe");
                        if (File.Exists(full)) return full;
                    }
                    catch { }
                }
            return null;
        }

        // Document -> Markdown éditable. --wrap=none : pas de retours à la
        // ligne artificiels qui perturberaient les règles typographiques.
        // -smart : sans ça, le writer de Pandoc échappe les guillemets
        // droits, les apostrophes et les points de suspension (\", \', \...)
        // pour qu'ils ne soient pas « corrigés » à la relecture — c'est
        // justement le travail de la nounou, et ces antislashs se
        // retrouvaient dans le texte. Même drapeau à l'export et à l'aperçu.
        // separateurs : les lignes qui ne contiennent qu'un séparateur de
        // texte (« *** ») reviennent échappées (\*\*\*) ; on les déséchappe
        // pour l'édition, EchapperSeparateurs les rétablit avant Pandoc.
        // Le Markdown demandé à Pandoc à l'import : sans « smart » (voir
        // ci-dessus) et sans aucune syntaxe d'attributs — un titre stylé
        // « Titre de Chapitre » dans Word ressortait en
        // « # Mouvement 21 {#mouvement-21 .Titre-de-Chapitre} », une
        // aberration pour un manuscrit ; ces marques ne servent à rien à
        // l'aller-retour (la chirurgie .docx garde les styles, et l'export
        // Pandoc ne sait de toute façon pas les rendre au document).
        // -raw_html/-raw_tex : un signet Word ou LibreOffice ressortait en
        // « <span id="anchor"></span> » collé au premier mot ; sans HTML
        // brut, ces spans sans contenu disparaissent (le texte, lui, reste).
        public const string FormatImport = "markdown-smart-header_attributes-auto_identifiers" +
            "-bracketed_spans-native_divs-native_spans-fenced_divs-raw_attribute" +
            "-link_attributes-inline_code_attributes-fenced_code_attributes" +
            "-raw_html-raw_tex";

        public static string ImporterEnMarkdown(string pandoc, string document,
            string[] separateurs)
        {
            var temp = Path.Combine(Path.GetTempPath(),
                "typonanny_" + Guid.NewGuid().ToString("N") + ".md");
            try
            {
                Executer(pandoc, "--wrap=none -t " + FormatImport + " -o \"" + temp +
                    "\" \"" + document + "\"");
                return DesechapperSeparateurs(Deciter(File.ReadAllText(temp)), separateurs);
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }

        // Le lecteur docx/odt de Pandoc prend tout paragraphe indenté
        // (retrait gauche, courant dans un manuscrit) pour une citation :
        // le texte entier ressort en « > ». Si la citation envahit la
        // majorité des lignes, ce n'en est pas une : on retire les chevrons
        // partout. Les vraies citations d'un texte normal (minoritaires)
        // restent telles quelles.
        public static string Deciter(string markdown)
        {
            var lignes = markdown.Replace("\r\n", "\n").Split('\n');
            var pleines = 0; var citees = 0;
            foreach (var l in lignes)
            {
                if (l.Trim().Length == 0) continue;
                pleines++;
                if (l.StartsWith(">")) citees++;
            }
            if (pleines == 0 || citees * 2 < pleines) return markdown;
            return Regex.Replace(markdown, @"(?m)^(?:>[ \t]?)+", "");
        }

        // Une ligne « \*\*\* » (séparateur échappé par Pandoc) -> « *** ».
        public static string DesechapperSeparateurs(string markdown, string[] separateurs)
        {
            if (separateurs == null || separateurs.Length == 0) return markdown;
            // (\r? : avec (?m), $ ne précède que \n — les fins de ligne
            // Windows laissent un \r qu'il faut laisser hors du corps)
            return Regex.Replace(markdown, @"(?m)^([ \t]*)(\S.*?)([ \t]*)(?=\r?$)", delegate(Match m)
            {
                var nu = m.Groups[2].Value.Replace("\\", "");
                foreach (var s in separateurs)
                    if (nu == s) return m.Groups[1].Value + s + m.Groups[3].Value;
                return m.Value;
            });
        }

        // L'inverse, avant de rendre le Markdown à Pandoc : une ligne qui
        // n'est qu'un séparateur redevient du texte échappé, sinon « *** »
        // serait lu comme un filet horizontal et « --- » comme un titre.
        public static string EchapperSeparateurs(string markdown, string[] separateurs)
        {
            if (separateurs == null || separateurs.Length == 0) return markdown;
            return Regex.Replace(markdown, @"(?m)^([ \t]*)(\S.*?)([ \t]*)(?=\r?$)", delegate(Match m)
            {
                foreach (var s in separateurs)
                    if (m.Groups[2].Value == s)
                    {
                        var sb = new StringBuilder();
                        foreach (var c in s)
                        {
                            if ("\\*_#-+>~=|`[]!<".IndexOf(c) >= 0) sb.Append('\\');
                            sb.Append(c);
                        }
                        return m.Groups[1].Value + sb + m.Groups[3].Value;
                    }
                return m.Value;
            });
        }

        // Markdown nettoyé -> document (.docx ou .odt selon l'extension de
        // la destination). Les insécables sont de simples caractères Unicode :
        // elles survivent à l'aller-retour. « referenceDoc » (optionnel) :
        // le document d'origine sert de gabarit de styles (--reference-doc),
        // pour que polices et titres restent ceux de la maison.
        public static void ExporterDepuisMarkdown(string pandoc, string markdown,
            string dest, string referenceDoc, string[] separateurs)
        {
            var temp = Path.Combine(Path.GetTempPath(),
                "typonanny_" + Guid.NewGuid().ToString("N") + ".md");
            try
            {
                File.WriteAllText(temp, EchapperSeparateurs(markdown, separateurs),
                    new UTF8Encoding(false));
                var gabarit = referenceDoc != null && File.Exists(referenceDoc)
                    ? "--reference-doc=\"" + referenceDoc + "\" " : "";
                Executer(pandoc, "--standalone -f markdown-smart " + gabarit +
                    "-o \"" + dest + "\" \"" + temp + "\"");
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }

        // ------------------------------------- chirurgie .docx (styles 100 %)
        // Le chemin royal : au lieu de repasser par Markdown (qui simplifie
        // la mise en forme), on copie le document et on corrige la typographie
        // DIRECTEMENT dans les nœuds de texte de word/document.xml. Chaque
        // paragraphe est nettoyé d'un bloc (les règles voient le texte
        // complet, même fragmenté en runs), puis le texte corrigé est
        // redistribué sur les runs d'origine par un diff caractère par
        // caractère : gras, couleurs, polices, styles — rien ne bouge.
        // Retourne le nombre de corrections appliquées.

        public static int ChirurgieDocx(string source, string dest, OptionsTypo o,
            HashSet<string> ligaturesOe, HashSet<string> ligaturesAe)
        {
            File.Copy(source, dest, true);
            var corrections = 0;
            using (var zip = ZipFile.Open(dest, ZipArchiveMode.Update))
            {
                var entry = zip.GetEntry("word/document.xml");
                if (entry == null)
                    throw new Exception("word/document.xml introuvable : ce .docx est inhabituel");
                string xml;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    xml = reader.ReadToEnd();

                xml = Regex.Replace(xml, @"<w:p\b[^>]*>[\s\S]*?</w:p>",
                    delegate(Match p)
                    {
                        int n;
                        var remplace = CorrigerParagraphe(p.Value, o,
                            ligaturesOe, ligaturesAe, out n);
                        corrections += n;
                        return remplace;
                    });

                var octets = new UTF8Encoding(false).GetBytes(xml);
                using (var sortie = entry.Open())
                {
                    sortie.SetLength(0);
                    sortie.Write(octets, 0, octets.Length);
                }
            }
            return corrections;
        }

        private static readonly Regex RxNoeudTexte =
            new Regex(@"<w:t(?:\s[^>]*)?>([\s\S]*?)</w:t>");

        private static string CorrigerParagraphe(string paragraphe, OptionsTypo o,
            HashSet<string> ligaturesOe, HashSet<string> ligaturesAe, out int corrections)
        {
            corrections = 0;
            var noeuds = RxNoeudTexte.Matches(paragraphe);
            if (noeuds.Count == 0) return paragraphe;

            var textes = new string[noeuds.Count];
            var ancien = new StringBuilder();
            for (var i = 0; i < noeuds.Count; i++)
            {
                textes[i] = DecoderEntites(noeuds[i].Groups[1].Value);
                ancien.Append(textes[i]);
            }
            if (ancien.Length == 0) return paragraphe;

            var resultat = Typo.Nettoyer(ancien.ToString(), o, ligaturesOe, ligaturesAe);
            var nouveau = resultat.Texte;
            if (nouveau == ancien.ToString()) return paragraphe;
            foreach (var kv in resultat.Compteurs) corrections += kv.Value;

            var sorties = Redistribuer(textes, ancien.ToString(), nouveau);

            var index = 0;
            return RxNoeudTexte.Replace(paragraphe, delegate(Match m)
            {
                var texte = sorties[index++];
                return "<w:t xml:space=\"preserve\">" + EncoderEntites(texte) + "</w:t>";
            });
        }

        // Répartit le texte corrigé sur les nœuds d'origine : les caractères
        // inchangés restent dans leur nœud (donc leur run, donc leur style),
        // les insertions se greffent sur le nœud du caractère précédent.
        private static string[] Redistribuer(string[] noeuds, string ancien, string nouveau)
        {
            var sorties = new StringBuilder[noeuds.Length];
            for (var i = 0; i < noeuds.Length; i++) sorties[i] = new StringBuilder();

            // propriétaire de chaque caractère de « ancien »
            var proprietaire = new int[ancien.Length];
            var pos = 0;
            for (var i = 0; i < noeuds.Length; i++)
                foreach (var c in noeuds[i]) proprietaire[pos++] = i;

            var ops = DiffCaracteres(ancien, nouveau);
            if (ops == null)
            {
                // repli (diff trop gros, ne devrait pas arriver) : tout le
                // texte corrigé dans le premier nœud — moins fin, jamais faux
                sorties[0].Append(nouveau);
            }
            else
            {
                var vieux = 0;
                var dernier = 0;
                foreach (var op in ops)
                {
                    if (op.Type == ' ')
                    {
                        dernier = proprietaire[vieux];
                        sorties[dernier].Append(op.Caractere);
                        vieux++;
                    }
                    else if (op.Type == '-')
                    {
                        dernier = proprietaire[vieux];
                        vieux++;
                    }
                    else sorties[dernier].Append(op.Caractere);
                }
            }

            var result = new string[noeuds.Length];
            for (var i = 0; i < noeuds.Length; i++) result[i] = sorties[i].ToString();
            return result;
        }

        public class OpCaractere
        {
            public char Type;       // ' ' inchangé, '-' supprimé, '+' ajouté
            public char Caractere;
            public OpCaractere(char type, char c) { Type = type; Caractere = c; }
        }

        // Diff de caractères par Myers (même algorithme que One di-version,
        // au caractère près). Les corrections typo sont locales : d reste
        // minuscule, c'est quasi instantané. null si ça diverge (garde-fou).
        // Public : l'aperçu s'en sert pour surligner les corrections.
        public static List<OpCaractere> DiffCaracteres(string a, string b)
        {
            var n = a.Length; var m = b.Length;
            var max = n + m;
            if (max == 0) return new List<OpCaractere>();
            var dMax = Math.Min(max, 800);

            var traces = new List<int[]>();
            var v = new int[2 * max + 1];
            var trouve = false;
            var dFinal = 0;
            for (var d = 0; d <= dMax && !trouve; d++)
            {
                traces.Add((int[])v.Clone());
                for (var k = -d; k <= d; k += 2)
                {
                    int x;
                    if (k == -d || (k != d && v[max + k - 1] < v[max + k + 1]))
                        x = v[max + k + 1];
                    else
                        x = v[max + k - 1] + 1;
                    var y = x - k;
                    while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                    v[max + k] = x;
                    if (x >= n && y >= m) { trouve = true; dFinal = d; break; }
                }
            }
            if (!trouve) return null;

            var ops = new List<OpCaractere>();
            var px = n; var py = m;
            for (var d = dFinal; d > 0; d--)
            {
                var vPrec = traces[d];
                var k = px - py;
                int kPrec;
                if (k == -d || (k != d && vPrec[max + k - 1] < vPrec[max + k + 1]))
                    kPrec = k + 1;
                else
                    kPrec = k - 1;
                var xPrec = vPrec[max + kPrec];
                var yPrec = xPrec - kPrec;
                while (px > xPrec && py > yPrec)
                { px--; py--; ops.Add(new OpCaractere(' ', a[px])); }
                if (kPrec == k + 1) { py--; ops.Add(new OpCaractere('+', b[py])); }
                else { px--; ops.Add(new OpCaractere('-', a[px])); }
            }
            while (px > 0 && py > 0)
            { px--; py--; ops.Add(new OpCaractere(' ', a[px])); }
            while (px > 0) { px--; ops.Add(new OpCaractere('-', a[px])); }
            while (py > 0) { py--; ops.Add(new OpCaractere('+', b[py])); }

            ops.Reverse();
            return ops;
        }

        private static string EncoderEntites(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static void Executer(string pandoc, string arguments)
        {
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = pandoc;
            psi.Arguments = arguments;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                var err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    var ligne = err.Trim().Split('\n')[0].Trim();
                    throw new Exception("Pandoc a échoué" +
                        (ligne.Length > 0 ? " : " + ligne : "."));
                }
            }
        }

        // Markdown -> fragment HTML (pour l'aperçu dans le navigateur) :
        // gras, italiques, exposants, listes… rendus par Pandoc.
        public static string MarkdownVersHtml(string pandoc, string markdown,
            string[] separateurs)
        {
            var tempMd = Path.Combine(Path.GetTempPath(),
                "typonanny_" + Guid.NewGuid().ToString("N") + ".md");
            var tempHtml = Path.ChangeExtension(tempMd, ".html");
            try
            {
                File.WriteAllText(tempMd, EchapperSeparateurs(markdown, separateurs),
                    new UTF8Encoding(false));
                // -raw_html : un manuscrit est du texte — une balise <script>
                // qui traîne doit s'afficher, pas s'exécuter dans l'aperçu.
                // -smart : l'aperçu montre le texte tel quel, sans retouche.
                Executer(pandoc, "--wrap=none -f markdown-raw_html-smart -t html -o \"" +
                    tempHtml + "\" \"" + tempMd + "\"");
                return File.ReadAllText(tempHtml);
            }
            finally
            {
                try { if (File.Exists(tempMd)) File.Delete(tempMd); } catch { }
                try { if (File.Exists(tempHtml)) File.Delete(tempHtml); } catch { }
            }
        }

        // Sans Pandoc : le texte brut du document, paragraphe par paragraphe
        // (docx et odt sont des ZIP contenant le texte en XML).
        public static string ExtraireTexteBrut(string document)
        {
            var ext = Path.GetExtension(document).ToLowerInvariant();
            var entree = ext == ".docx" ? "word/document.xml" : "content.xml";
            string xml = null;
            using (var fs = new FileStream(document, FileMode.Open, FileAccess.Read))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                foreach (var entry in zip.Entries)
                    if (string.Equals(entry.FullName, entree, StringComparison.OrdinalIgnoreCase))
                    {
                        using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                            xml = reader.ReadToEnd();
                        break;
                    }
            if (xml == null)
                throw new Exception("document illisible : " + entree + " introuvable");

            xml = Regex.Replace(xml, @"</w:p>|</text:p>|</text:h>", "\n");
            xml = Regex.Replace(xml, @"<w:tab[^>]*/>|<text:tab[^>]*/>", "\t");
            xml = Regex.Replace(xml, @"<w:br[^>]*/>|<text:line-break[^>]*/>", "\n");
            xml = Regex.Replace(xml, @"<[^>]+>", "");
            xml = DecoderEntites(xml);
            // au plus une ligne vide entre paragraphes
            xml = Regex.Replace(xml, @"\n{3,}", "\n\n");
            return xml.Trim();
        }

        private static string DecoderEntites(string s)
        {
            s = Regex.Replace(s, @"&#x([0-9A-Fa-f]+);", delegate(Match m)
            {
                return char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16));
            });
            s = Regex.Replace(s, @"&#(\d+);", delegate(Match m)
            {
                return char.ConvertFromUtf32(int.Parse(m.Groups[1].Value));
            });
            return s.Replace("&lt;", "<").Replace("&gt;", ">")
                    .Replace("&quot;", "\"").Replace("&apos;", "'")
                    .Replace("&amp;", "&");
        }
    }

}
