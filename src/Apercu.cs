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

// Apercu.cs — l'aperçu HTML ouvert dans le navigateur : vue
// « Corrections » (diff surligné au caractère près) et vue « Mise en
// page » (rendu riche via Pandoc, mini-Markdown maison en repli).

namespace Typonanny
{
    // --------------------------------------------- aperçu (navigateur)
    // Le résultat se relit dans le navigateur, en deux vues :
    // « Corrections » (par défaut) surligne chaque retouche à la manière
    // de One di-version — supprimé barré rouge, inséré vert — grâce au
    // diff caractère par caractère ; « Mise en page » rend le texte final
    // proprement (gras, italiques, exposants via Pandoc, repli maison
    // sinon), cadratins et insécables affichés tels quels (UTF-8).

    public static class Apercu
    {
        // avant = le texte d'origine, corrige = le texte corrigé (même en
        // mode signalement), finalTexte = ce que Copier/Enregistrer
        // produiront (identique à corrige, sauf en mode signalement).
        public static string Construire(string nomDocument, string avant,
            string corrige, string finalTexte, string pandoc, string[] separateurs)
        {
            string mise = null;
            if (pandoc != null)
                try { mise = PontDocuments.MarkdownVersHtml(pandoc, finalTexte, separateurs); }
                catch { }
            if (mise == null) mise = RenduSommaire(finalTexte);

            var corrections = ConstruireDiff(avant, corrige);

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>\n<html lang=\"fr\">\n<head>\n");
            sb.Append("<meta charset=\"utf-8\">\n");
            sb.Append("<title>Stargazer — Typonanny — ").Append(Echapper(nomDocument))
              .Append("</title>\n<style>\n");
            sb.Append("body{background:#0b1026;color:#e6e6f0;margin:0;padding:0}\n");
            sb.Append("header{background:#131a33;border-bottom:1px solid #2a3358;");
            sb.Append("padding:1.2em 2em;font-family:'Segoe UI',sans-serif}\n");
            sb.Append("header h1{color:#f4d77a;font-size:1.25em;margin:0}\n");
            sb.Append("header p{color:#9aa3c0;margin:.3em 0 0;font-size:.95em}\n");
            sb.Append("nav{margin-top:.8em}\n");
            sb.Append("nav button{background:#131a33;color:#e6e6f0;border:1px solid #2a3358;");
            sb.Append("border-radius:8px;padding:.35em 1em;margin-right:.5em;cursor:pointer;");
            sb.Append("font-family:'Segoe UI',sans-serif}\n");
            sb.Append("nav button.actif{background:#d4af37;color:#14141e;border-color:#d4af37}\n");
            sb.Append("main{max-width:44em;margin:2.5em auto;padding:0 1.5em;");
            sb.Append("font-family:Georgia,'Times New Roman',serif;font-size:1.05em;");
            sb.Append("line-height:1.75}\n");
            sb.Append("main h1,main h2,main h3,main h4{color:#f4d77a;");
            sb.Append("font-family:'Segoe UI',sans-serif;line-height:1.3}\n");
            sb.Append("main a{color:#7aa2f7}\n");
            sb.Append("blockquote{border-left:3px solid #d4af37;margin-left:0;");
            sb.Append("padding-left:1em;color:#9aa3c0}\n");
            sb.Append("code{background:#131a33;padding:.1em .3em;border-radius:4px}\n");
            sb.Append("pre.diff{white-space:pre-wrap;font-family:inherit;margin:0}\n");
            sb.Append("del{background:#461a20;color:#e66e78;text-decoration:line-through}\n");
            sb.Append("ins{background:#183a22;color:#98c379;text-decoration:none}\n");
            sb.Append(".ctx{color:#9aa3c0}\n");
            sb.Append("</style>\n</head>\n<body>\n<header>\n");
            sb.Append("<h1>⭐ Stargazer — Typonanny</h1>\n");
            sb.Append("<p>").Append(Echapper(nomDocument)).Append("</p>\n");
            sb.Append("<nav>\n");
            sb.Append("<button id=\"bc\" class=\"actif\" onclick=\"voir('c')\">Corrections</button>\n");
            sb.Append("<button id=\"bm\" onclick=\"voir('m')\">Mise en page</button>\n");
            sb.Append("</nav>\n</header>\n");
            sb.Append("<main id=\"corrections\">\n").Append(corrections).Append("\n</main>\n");
            sb.Append("<main id=\"mise\" style=\"display:none\">\n").Append(mise).Append("\n</main>\n");
            sb.Append("<script>\nfunction voir(v){\n");
            sb.Append("document.getElementById('corrections').style.display=v=='c'?'block':'none';\n");
            sb.Append("document.getElementById('mise').style.display=v=='m'?'block':'none';\n");
            sb.Append("document.getElementById('bc').className=v=='c'?'actif':'';\n");
            sb.Append("document.getElementById('bm').className=v=='m'?'actif':'';\n");
            sb.Append("}\n</script>\n</body>\n</html>\n");
            return sb.ToString();
        }

        // La vue « Corrections » : le nettoyage ne touche jamais aux sauts
        // de ligne, donc chaque ligne se compare à son homologue — diff
        // caractère par caractère sur les lignes modifiées, le reste en doux.
        private static string ConstruireDiff(string avant, string corrige)
        {
            var sb = new StringBuilder();
            sb.Append("<pre class=\"diff\">");
            if (avant == corrige)
            {
                sb.Append("<span class=\"ctx\">Aucune correction : ce texte était " +
                    "déjà impeccable.</span>\n");
                sb.Append(Echapper(corrige));
                sb.Append("</pre>");
                return sb.ToString();
            }
            var la = avant.Replace("\r\n", "\n").Split('\n');
            var lc = corrige.Replace("\r\n", "\n").Split('\n');
            if (la.Length == lc.Length)
            {
                for (var i = 0; i < la.Length; i++)
                {
                    if (la[i] == lc[i])
                        sb.Append("<span class=\"ctx\">").Append(Echapper(la[i]))
                          .Append("</span>\n");
                    else
                    {
                        RenduLigneDiff(sb, la[i], lc[i]);
                        sb.Append('\n');
                    }
                }
            }
            else RenduLigneDiff(sb, avant, corrige);   // prudence (ne devrait pas arriver)
            sb.Append("</pre>");
            return sb.ToString();
        }

        private static void RenduLigneDiff(StringBuilder sb, string a, string b)
        {
            var ops = PontDocuments.DiffCaracteres(a, b);
            if (ops == null)   // diff trop gros : montrer le nouveau, marqué
            {
                sb.Append("<ins>").Append(Echapper(b)).Append("</ins>");
                return;
            }
            // regrouper les opérations contiguës de même type
            var i = 0;
            while (i < ops.Count)
            {
                var type = ops[i].Type;
                var bloc = new StringBuilder();
                while (i < ops.Count && ops[i].Type == type)
                { bloc.Append(ops[i].Caractere); i++; }
                var texte = Echapper(bloc.ToString());
                if (type == '-') sb.Append("<del>").Append(texte).Append("</del>");
                else if (type == '+') sb.Append("<ins>").Append(texte).Append("</ins>");
                else sb.Append(texte);
            }
        }

        // Repli sans Pandoc : l'essentiel du Markdown (titres, gras,
        // italiques, exposants/indices, paragraphes), texte échappé d'abord.
        private static string RenduSommaire(string texte)
        {
            var t = Echapper(texte.Replace("\r\n", "\n"));
            for (var niveau = 6; niveau >= 1; niveau--)
            {
                var diese = new string('#', niveau);
                t = Regex.Replace(t, @"(?m)^" + diese + @"\s+(.+)$",
                    "<h" + niveau + ">$1</h" + niveau + ">");
            }
            t = Regex.Replace(t, @"\*\*([^*\n]+)\*\*", "<strong>$1</strong>");
            t = Regex.Replace(t, @"(?<![\w*])\*([^*\n]+)\*(?![\w*])", "<em>$1</em>");
            t = Regex.Replace(t, @"\^([^\^\s]+)\^", "<sup>$1</sup>");
            t = Regex.Replace(t, @"(?<!~)~([^~\s]+)~(?!~)", "<sub>$1</sub>");

            var sb = new StringBuilder();
            foreach (var bloc in Regex.Split(t, @"\n\s*\n"))
            {
                var b = bloc.Trim();
                if (b.Length == 0) continue;
                if (b.StartsWith("<h")) sb.Append(b).Append('\n');
                else sb.Append("<p>").Append(b.Replace("\n", "<br>\n")).Append("</p>\n");
            }
            return sb.ToString();
        }

        private static string Echapper(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }

}
