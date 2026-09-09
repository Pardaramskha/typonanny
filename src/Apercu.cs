using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Apercu.cs — l'aperçu HTML ouvert dans le navigateur : vue
// « Corrections » (diff surligné au caractère près, chaque retouche
// attribuée à sa règle pour le filtre) et vue « Mise en page » (rendu
// riche via Pandoc, mini-Markdown maison en repli). Un « paper » flottant
// en haut à droite règle le thème (sombre / clair), les cinq couleurs, et
// les règles dont on veut voir les corrections — le tout mémorisé dans
// le navigateur (localStorage).

namespace Typonanny
{
    public static class Apercu
    {
        // avant = le texte d'origine, etapes = le texte après chaque règle
        // (cf. Typo.Nettoyer), finalTexte = ce que Copier/Enregistrer
        // produiront (le texte corrigé, sauf en mode signalement).
        public static string Construire(string nomDocument, string avant,
            List<Etape> etapes, string finalTexte, string pandoc, string[] separateurs)
        {
            string mise = null;
            if (pandoc != null)
                try { mise = PontDocuments.MarkdownVersHtml(pandoc, finalTexte, separateurs); }
                catch { }
            if (mise == null) mise = RenduSommaire(Desechapper(finalTexte));

            var regles = new List<Etape>();
            var corrections = ConstruireDiff(avant, etapes, regles);

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>\n<html lang=\"fr\">\n<head>\n");
            sb.Append("<meta charset=\"utf-8\">\n");
            sb.Append("<title>Stargazer — Typonanny — ").Append(Echapper(nomDocument))
              .Append("</title>\n<style>\n");
            sb.Append(Style());
            sb.Append("</style>\n</head>\n<body>\n<header>\n");
            sb.Append("<h1>⭐ Stargazer — Typonanny</h1>\n");
            sb.Append("<p>").Append(Echapper(nomDocument)).Append("</p>\n");
            sb.Append("<nav>\n");
            sb.Append("<button id=\"bc\" class=\"actif\" onclick=\"voir('c')\">Corrections</button>\n");
            sb.Append("<button id=\"bm\" onclick=\"voir('m')\">Mise en page</button>\n");
            sb.Append("</nav>\n</header>\n");
            sb.Append(Papier(regles));
            sb.Append("<main id=\"corrections\">\n").Append(corrections).Append("\n</main>\n");
            sb.Append("<main id=\"mise\" style=\"display:none\">\n").Append(mise).Append("\n</main>\n");
            sb.Append("<script>\n").Append(Script()).Append("</script>\n</body>\n</html>\n");
            return sb.ToString();
        }

        // ------------------------------------------------------ habillage

        private static string Style()
        {
            var sb = new StringBuilder();
            // Les couleurs sont des variables : le paper les change en direct.
            sb.Append(":root{--fond:#0b1026;--texte:#e6e6f0;--intact:#9aa3c0;--ajout:#98c379;--suppr:#e66e78;");
            sb.Append("--panneau:#131a33;--bordure:#2a3358;--or:#d4af37;--orclair:#f4d77a;--lien:#7aa2f7}\n");
            sb.Append("body{background:var(--fond);color:var(--texte);margin:0;padding:0}\n");
            sb.Append("header{background:var(--panneau);border-bottom:1px solid var(--bordure);");
            sb.Append("padding:1.2em 2em;font-family:'Segoe UI',sans-serif}\n");
            sb.Append("header h1{color:var(--orclair);font-size:1.25em;margin:0}\n");
            sb.Append("header p{color:var(--intact);margin:.3em 0 0;font-size:.95em}\n");
            sb.Append("nav{margin-top:.8em}\n");
            sb.Append("nav button,#papier button{background:var(--panneau);color:var(--texte);border:1px solid var(--bordure);");
            sb.Append("border-radius:8px;padding:.35em 1em;margin-right:.5em;cursor:pointer;");
            sb.Append("font-family:'Segoe UI',sans-serif;font-size:.95em}\n");
            sb.Append("nav button.actif,#papier button.actif{background:var(--or);color:#14141e;border-color:var(--or)}\n");
            sb.Append("main{max-width:44em;margin:2.5em auto;padding:0 1.5em;");
            sb.Append("font-family:Georgia,'Times New Roman',serif;font-size:1.05em;line-height:1.75}\n");
            sb.Append("main h1,main h2,main h3,main h4{color:var(--orclair);font-family:'Segoe UI',sans-serif;line-height:1.3}\n");
            sb.Append("main a{color:var(--lien)}\n");
            sb.Append("blockquote{border-left:3px solid var(--or);margin-left:0;padding-left:1em;color:var(--intact)}\n");
            sb.Append("code{background:var(--panneau);padding:.1em .3em;border-radius:4px}\n");
            sb.Append("pre.diff{white-space:pre-wrap;font-family:inherit;margin:0}\n");
            sb.Append("del{background:color-mix(in srgb,var(--suppr) 22%,transparent);color:var(--suppr);text-decoration:line-through}\n");
            sb.Append("ins{background:color-mix(in srgb,var(--ajout) 22%,transparent);color:var(--ajout);text-decoration:none}\n");
            sb.Append(".ctx{color:var(--intact)}\n");
            // le paper flottant
            sb.Append("#papier{position:fixed;top:1em;right:1em;width:16em;background:var(--panneau);");
            sb.Append("border:1px solid var(--bordure);border-radius:12px;box-shadow:0 12px 40px rgba(0,0,0,.35);");
            sb.Append("font-family:'Segoe UI',sans-serif;font-size:.9em;color:var(--texte);z-index:10}\n");
            sb.Append("#papier h2{font-size:1em;margin:0;padding:.7em 1em;cursor:pointer;color:var(--orclair);user-select:none}\n");
            sb.Append("#papier h2:after{content:'▾';float:right;color:var(--intact)}\n");
            sb.Append("#papier.plie h2:after{content:'▸'}\n");
            sb.Append("#papier.plie .corps{display:none}\n");
            sb.Append("#papier .corps{padding:0 1em 1em;max-height:70vh;overflow:auto}\n");
            sb.Append("#papier h3{font-size:.85em;text-transform:uppercase;letter-spacing:.04em;color:var(--intact);margin:1em 0 .4em}\n");
            sb.Append("#papier h3:first-child{margin-top:.2em}\n");
            sb.Append("#papier .themes button{margin:0 .4em 0 0;padding:.3em .8em}\n");
            sb.Append("#papier label{display:flex;align-items:center;justify-content:space-between;padding:.2em 0;cursor:pointer}\n");
            sb.Append("#papier input[type=color]{width:2.2em;height:1.6em;border:1px solid var(--bordure);border-radius:6px;background:none;padding:0;cursor:pointer}\n");
            sb.Append("#papier input[type=checkbox]{margin:0 .5em 0 0;accent-color:var(--or)}\n");
            sb.Append("#papier label.filtre{justify-content:flex-start}\n");
            sb.Append("#papier .n{margin-left:auto;color:var(--intact);font-size:.9em}\n");
            sb.Append("#papier .reset{margin-top:.6em;padding:.3em .8em;font-size:.9em}\n");
            sb.Append("#papier .vide{color:var(--intact);font-style:italic}\n");
            sb.Append("@media (max-width:70em){#papier{position:static;width:auto;margin:1em 1.5em 0}}\n");
            return sb.ToString();
        }

        // Le paper : thème, couleurs, filtre des règles.
        private static string Papier(List<Etape> regles)
        {
            var sb = new StringBuilder();
            sb.Append("<aside id=\"papier\">\n<h2 onclick=\"plier()\">Affichage</h2>\n<div class=\"corps\">\n");
            sb.Append("<h3>Thème</h3>\n<div class=\"themes\">");
            sb.Append("<button id=\"th-sombre\" onclick=\"theme('sombre')\">Sombre</button>");
            sb.Append("<button id=\"th-clair\" onclick=\"theme('clair')\">Clair</button></div>\n");
            sb.Append("<h3>Couleurs</h3>\n");
            var couleurs = new string[,] {
                { "fond", "Fond" }, { "texte", "Texte modifié" }, { "intact", "Texte intact" },
                { "ajout", "Ajout" }, { "suppr", "Suppression" } };
            for (var i = 0; i < couleurs.GetLength(0); i++)
                sb.Append("<label>").Append(couleurs[i, 1])
                  .Append("<input type=\"color\" id=\"c-").Append(couleurs[i, 0])
                  .Append("\" oninput=\"couleur('").Append(couleurs[i, 0]).Append("',this.value)\"></label>\n");
            sb.Append("<button class=\"reset\" onclick=\"reinit()\">Couleurs du thème</button>\n");
            sb.Append("<h3>Corrections affichées</h3>\n");
            if (regles.Count == 0)
                sb.Append("<div class=\"vide\">Aucune correction.</div>\n");
            foreach (var e in regles)
                sb.Append("<label class=\"filtre\"><input type=\"checkbox\" checked data-c=\"")
                  .Append(e.Cle).Append("\" onchange=\"filtrer()\">").Append(Echapper(e.Libelle))
                  .Append("<span class=\"n\">").Append(e.Texte).Append("</span></label>\n");
            sb.Append("</div>\n</aside>\n");
            return sb.ToString();
        }

        private static string Script()
        {
            var sb = new StringBuilder();
            sb.Append("var PRESETS={sombre:{fond:'#0b1026',texte:'#e6e6f0',intact:'#9aa3c0',ajout:'#98c379',suppr:'#e66e78',");
            sb.Append("panneau:'#131a33',bordure:'#2a3358',or:'#d4af37',orclair:'#f4d77a',lien:'#7aa2f7'},");
            sb.Append("clair:{fond:'#fbf8f1',texte:'#1f2430',intact:'#6b7280',ajout:'#2e7d32',suppr:'#c62828',");
            sb.Append("panneau:'#ffffff',bordure:'#d9d4c7',or:'#b8912a',orclair:'#8a6a12',lien:'#2f5fc4'}};\n");
            sb.Append("var CLES=['fond','texte','intact','ajout','suppr'];\n");
            sb.Append("var etat={theme:'sombre',couleurs:{},off:[],plie:false};\n");
            sb.Append("try{var s=localStorage.getItem('typonanny.apercu');if(s)etat=Object.assign(etat,JSON.parse(s));}catch(e){}\n");
            sb.Append("function sauver(){try{localStorage.setItem('typonanny.apercu',JSON.stringify(etat));}catch(e){}}\n");
            sb.Append("function appliquer(){var p=PRESETS[etat.theme]||PRESETS.sombre;var r=document.documentElement.style;\n");
            sb.Append("for(var k in p)r.setProperty('--'+k,p[k]);\n");
            sb.Append("CLES.forEach(function(k){var v=etat.couleurs[k]||p[k];r.setProperty('--'+k,v);document.getElementById('c-'+k).value=v;});\n");
            sb.Append("document.getElementById('th-sombre').className=etat.theme=='sombre'?'actif':'';\n");
            sb.Append("document.getElementById('th-clair').className=etat.theme=='clair'?'actif':'';\n");
            sb.Append("document.getElementById('papier').className=etat.plie?'plie':'';\n");
            sb.Append("var css='';document.querySelectorAll('#papier input[type=checkbox]').forEach(function(c){var k=c.getAttribute('data-c');\n");
            sb.Append("c.checked=etat.off.indexOf(k)<0;if(!c.checked)css+='ins[data-c=\"'+k+'\"]{background:none;color:inherit}del[data-c=\"'+k+'\"]{display:none}\\n';});\n");
            sb.Append("document.getElementById('filtre').textContent=css;}\n");
            sb.Append("function theme(t){etat.theme=t;etat.couleurs={};sauver();appliquer();}\n");
            sb.Append("function couleur(k,v){etat.couleurs[k]=v;sauver();appliquer();}\n");
            sb.Append("function reinit(){etat.couleurs={};sauver();appliquer();}\n");
            sb.Append("function plier(){etat.plie=!etat.plie;sauver();appliquer();}\n");
            sb.Append("function filtrer(){etat.off=[];document.querySelectorAll('#papier input[type=checkbox]').forEach(function(c){if(!c.checked)etat.off.push(c.getAttribute('data-c'));});sauver();appliquer();}\n");
            sb.Append("function voir(v){\n");
            sb.Append("document.getElementById('corrections').style.display=v=='c'?'block':'none';\n");
            sb.Append("document.getElementById('mise').style.display=v=='m'?'block':'none';\n");
            sb.Append("document.getElementById('bc').className=v=='c'?'actif':'';\n");
            sb.Append("document.getElementById('bm').className=v=='m'?'actif':'';\n}\n");
            sb.Append("var st=document.createElement('style');st.id='filtre';document.head.appendChild(st);appliquer();\n");
            return sb.ToString();
        }

        // ------------------------------------------------- vue Corrections
        // Le nettoyage ne touche jamais aux sauts de ligne : chaque ligne se
        // compare à son homologue, étape par étape (une étape = une règle),
        // pour que chaque retouche porte le nom de sa règle. « regles »
        // reçoit la liste des règles rencontrées avec leur nombre de
        // retouches (dans Texte), pour le filtre du paper.
        private static string ConstruireDiff(string avant, List<Etape> etapes, List<Etape> regles)
        {
            var sb = new StringBuilder();
            sb.Append("<pre class=\"diff\">");
            var corrige = etapes.Count > 0 ? etapes[etapes.Count - 1].Texte : avant;
            if (avant == corrige)
            {
                sb.Append("<span class=\"ctx\">Aucune correction : ce texte était " +
                    "déjà impeccable.</span>\n");
                sb.Append(Echapper(Desechapper(corrige)));
                sb.Append("</pre>");
                return sb.ToString();
            }
            var comptes = new Dictionary<string, int>();
            var libelles = new Dictionary<string, string>();
            var ordre = new List<string>();
            var la = Desechapper(avant).Replace("\r\n", "\n").Split('\n');
            var lignesEtapes = new List<string[]>();
            var coherent = true;
            foreach (var e in etapes)
            {
                var l = Desechapper(e.Texte).Replace("\r\n", "\n").Split('\n');
                if (l.Length != la.Length) coherent = false;
                lignesEtapes.Add(l);
                if (!libelles.ContainsKey(e.Cle)) { libelles[e.Cle] = e.Libelle; ordre.Add(e.Cle); comptes[e.Cle] = 0; }
            }
            if (coherent)
            {
                for (var i = 0; i < la.Length; i++)
                {
                    var items = new List<Item>();
                    foreach (var c in la[i]) items.Add(new Item(c, ' ', null));
                    var change = false;
                    for (var k = 0; k < etapes.Count; k++)
                    {
                        var cible = lignesEtapes[k][i];
                        if (Courant(items) == cible) continue;
                        change = true;
                        Composer(items, cible, etapes[k].Cle);
                    }
                    if (!change)
                        sb.Append("<span class=\"ctx\">").Append(Echapper(la[i])).Append("</span>\n");
                    else
                    {
                        Rendre(sb, items, comptes);
                        sb.Append('\n');
                    }
                }
            }
            else
            {
                // prudence (ne devrait pas arriver) : un seul diff, sans règle
                libelles["autre"] = "Corrections"; ordre.Add("autre"); comptes["autre"] = 0;
                var items = new List<Item>();
                foreach (var c in Desechapper(avant).Replace("\r\n", "\n")) items.Add(new Item(c, ' ', null));
                Composer(items, Desechapper(corrige).Replace("\r\n", "\n"), "autre");
                Rendre(sb, items, comptes);
            }
            sb.Append("</pre>");
            foreach (var cle in ordre)
            {
                if (comptes[cle] == 0) continue;
                var e = new Etape();
                e.Cle = cle; e.Libelle = libelles[cle];
                e.Texte = comptes[cle].ToString(CultureInfo.InvariantCulture);
                regles.Add(e);
            }
            return sb.ToString();
        }

        // Un caractère de la ligne composée : intact (' '), inséré ('+') ou
        // supprimé ('-'), et la règle responsable.
        private class Item
        {
            public char C; public char Kind; public string Cle;
            public Item(char c, char kind, string cle) { C = c; Kind = kind; Cle = cle; }
        }

        private static string Courant(List<Item> items)
        {
            var sb = new StringBuilder(items.Count);
            foreach (var it in items) if (it.Kind != '-') sb.Append(it.C);
            return sb.ToString();
        }

        // Applique le diff « texte courant -> cible » sur la ligne composée,
        // en attribuant les retouches à « cle ». Un caractère inséré par une
        // règle précédente puis retiré par celle-ci disparaît simplement.
        private static void Composer(List<Item> items, string cible, string cle)
        {
            var courant = Courant(items);
            var ops = PontDocuments.DiffCaracteres(courant, cible);
            if (ops == null)
            {
                // diff trop gros : tout l'ancien barré, tout le nouveau inséré
                foreach (var it in items) if (it.Kind == '+') it.Kind = '-';
                items.RemoveAll(delegate(Item it) { return it.Kind == '+'; });
                foreach (var it in items) { it.Kind = '-'; if (it.Cle == null) it.Cle = cle; }
                foreach (var c in cible) items.Add(new Item(c, '+', cle));
                return;
            }
            var pos = 0;   // index dans items (on saute les supprimés)
            foreach (var op in ops)
            {
                while (pos < items.Count && items[pos].Kind == '-') pos++;
                if (op.Type == ' ') { pos++; }   // inchangé
                else if (op.Type == '-')
                {
                    if (pos >= items.Count) continue;
                    if (items[pos].Kind == '+') items.RemoveAt(pos);
                    else { items[pos].Kind = '-'; items[pos].Cle = cle; pos++; }
                }
                else
                {
                    items.Insert(pos, new Item(op.Caractere, '+', cle));
                    pos++;
                }
            }
        }

        // Regroupe les caractères contigus de même nature et de même règle.
        private static void Rendre(StringBuilder sb, List<Item> items, Dictionary<string, int> comptes)
        {
            var i = 0;
            while (i < items.Count)
            {
                var kind = items[i].Kind; var cle = items[i].Cle;
                var bloc = new StringBuilder();
                while (i < items.Count && items[i].Kind == kind && items[i].Cle == cle)
                { bloc.Append(items[i].C); i++; }
                var texte = Echapper(bloc.ToString());
                if (kind == ' ') { sb.Append(texte); continue; }
                var balise = kind == '-' ? "del" : "ins";
                sb.Append('<').Append(balise).Append(" data-c=\"").Append(cle ?? "autre").Append("\">")
                  .Append(texte).Append("</").Append(balise).Append('>');
                if (cle != null && comptes.ContainsKey(cle)) comptes[cle]++;
            }
        }

        // ------------------------------------------------- vue Mise en page

        // Les antislashs d'échappement du Markdown (\#, 1\., \*) sont des
        // marques de transport : indispensables à l'aller-retour .docx, mais
        // elles n'ont rien à faire dans un aperçu.
        public static string Desechapper(string texte)
        {
            return Regex.Replace(texte, @"\\([\\*_#+>~=|`\[\]!<.\-])", "$1");
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
