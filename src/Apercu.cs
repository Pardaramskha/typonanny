using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Apercu.cs — l'aperçu HTML ouvert dans le navigateur : UNE vue, le texte
// mis en page (titres, gras, italiques, listes, citations, séparateurs
// centrés) avec chaque retouche surlignée au caractère près et attribuée
// à sa règle (pour le filtre). Un « paper » flottant en haut à droite
// règle le thème (sombre / clair), les cinq couleurs, et les règles dont
// on veut voir les corrections — le tout mémorisé dans le navigateur
// (localStorage). Les antislashs d'échappement du Markdown n'y
// apparaissent jamais.

namespace Typonanny
{
    public static class Apercu
    {
        // avant = le texte d'origine, etapes = le texte après chaque règle
        // (cf. Typo.Nettoyer), separateurs = les séparateurs de texte
        // protégés (rendus centrés).
        public static string Construire(string nomDocument, string avant,
            List<Etape> etapes, string[] separateurs)
        {
            var regles = new List<Etape>();
            var lignes = ConstruireDiff(avant, etapes, regles);
            var corps = MettreEnPage(lignes, separateurs);

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>\n<html lang=\"fr\">\n<head>\n");
            sb.Append("<meta charset=\"utf-8\">\n");
            sb.Append("<title>Stargazer — Typonanny — ").Append(Echapper(nomDocument))
              .Append("</title>\n<style>\n");
            sb.Append(Style());
            sb.Append("</style>\n</head>\n<body>\n<header>\n");
            sb.Append("<h1>⭐ Stargazer — Typonanny</h1>\n");
            sb.Append("<p>").Append(Echapper(nomDocument)).Append("</p>\n");
            sb.Append("</header>\n");
            sb.Append(Papier(regles));
            sb.Append("<main>\n").Append(corps).Append("\n</main>\n");
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
            sb.Append("#papier button{background:var(--panneau);color:var(--texte);border:1px solid var(--bordure);");
            sb.Append("border-radius:8px;padding:.35em 1em;margin-right:.5em;cursor:pointer;");
            sb.Append("font-family:'Segoe UI',sans-serif;font-size:.95em}\n");
            sb.Append("#papier button.actif{background:var(--or);color:#14141e;border-color:var(--or)}\n");
            sb.Append("main{max-width:44em;margin:2.5em auto;padding:0 1.5em;");
            sb.Append("font-family:Georgia,'Times New Roman',serif;font-size:1.05em;line-height:1.75}\n");
            // pre-wrap : les espaces (doubles, fines, insécables) se voient tels quels
            sb.Append("main p,main li,main h1,main h2,main h3,main h4,main h5,main h6{white-space:pre-wrap}\n");
            sb.Append("main h1,main h2,main h3,main h4,main h5,main h6{color:var(--orclair);font-family:'Segoe UI',sans-serif;line-height:1.3}\n");
            sb.Append("main a{color:var(--lien)}\n");
            sb.Append("blockquote{border-left:3px solid var(--or);margin-left:0;padding-left:1em;color:var(--intact)}\n");
            sb.Append("code{background:var(--panneau);padding:.1em .3em;border-radius:4px;font-size:.9em}\n");
            sb.Append("p.sep{text-align:center;letter-spacing:.4em;color:var(--intact)}\n");
            sb.Append("hr{border:0;border-top:1px solid var(--bordure);margin:1.5em 0}\n");
            sb.Append("p.note{color:var(--intact);font-style:italic}\n");
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
            sb.Append("#papier .rien{color:var(--intact);padding:.15em 0;opacity:.75}\n");
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
            // les règles qui n'ont rien eu à corriger : dites, pour qu'on ne
            // les cherche pas (un texte venu de Word a déjà ses apostrophes
            // courbes, par exemple)
            var vues = new HashSet<string>();
            foreach (var e in regles) vues.Add(e.Cle);
            var rien = new StringBuilder();
            for (var i = 0; i < ToutesLesRegles.GetLength(0); i++)
                if (!vues.Contains(ToutesLesRegles[i, 0]))
                    rien.Append("<div class=\"rien\">").Append(ToutesLesRegles[i, 1]).Append("</div>\n");
            if (rien.Length > 0)
                sb.Append("<h3>Rien à corriger</h3>\n").Append(rien);
            sb.Append("</div>\n</aside>\n");
            return sb.ToString();
        }

        // Les règles, dans l'ordre du moteur (mêmes clés que Typo.Etaper).
        private static readonly string[,] ToutesLesRegles = new string[,] {
            { "espaces", "Espaces" }, { "apostrophes", "Apostrophes courbes" },
            { "ellipses", "Points de suspension et « etc. »" }, { "guillemets", "Guillemets français" },
            { "tiretsDialogue", "Tirets de dialogue" }, { "intervalles", "Intervalles" },
            { "insecables", "Insécables de ponctuation" }, { "insecablesUnites", "Insécables d'unités" },
            { "milliers", "Milliers en fine" }, { "ligaturesOe", "Ligatures œ" },
            { "ligaturesAe", "Ligatures æ" }, { "dimensions", "Dimensions" }, { "ordinaux", "Ordinaux" } };

        private static string Script()
        {
            var sb = new StringBuilder();
            sb.Append("var PRESETS={sombre:{fond:'#0b1026',texte:'#e6e6f0',intact:'#9aa3c0',ajout:'#98c379',suppr:'#e66e78',");
            sb.Append("panneau:'#131a33',bordure:'#2a3358',or:'#d4af37',orclair:'#f4d77a',lien:'#7aa2f7'},");
            sb.Append("clair:{fond:'#fbf8f1',texte:'#1f2430',intact:'#6b7280',ajout:'#2e7d32',suppr:'#c62828',");
            sb.Append("panneau:'#ffffff',bordure:'#d9d4c7',or:'#b8912a',orclair:'#8a6a12',lien:'#2f5fc4'}};\n");
            sb.Append("var CLES=['fond','texte','intact','ajout','suppr'];\n");
            sb.Append("var etat={theme:'sombre',couleurs:{},off:[],plie:false};\n");
            // le thème et les couleurs se retiennent, PAS le filtre : un aperçu
            // s'ouvre toujours avec toutes les corrections visibles
            sb.Append("try{var s=localStorage.getItem('typonanny.apercu');if(s)etat=Object.assign(etat,JSON.parse(s));}catch(e){}\netat.off=[];\n");
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
            sb.Append("var st=document.createElement('style');st.id='filtre';document.head.appendChild(st);appliquer();\n");
            return sb.ToString();
        }

        // ------------------------------------------------------ le diff
        // Le nettoyage ne touche jamais aux sauts de ligne : chaque ligne se
        // compare à son homologue, étape par étape (une étape = une règle),
        // pour que chaque retouche porte le nom de sa règle. « regles »
        // reçoit la liste des règles rencontrées avec leur nombre de
        // retouches (dans Texte), pour le filtre du paper.

        // Une ligne du résultat : son HTML (texte échappé + <ins>/<del>) et
        // son texte final (pour reconnaître titres, listes, séparateurs…).
        private class Ligne
        {
            public string Html; public string Texte; public bool Change;
        }

        private static List<Ligne> ConstruireDiff(string avant, List<Etape> etapes, List<Etape> regles)
        {
            var resultat = new List<Ligne>();
            var corrige = etapes.Count > 0 ? etapes[etapes.Count - 1].Texte : avant;
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
                    var ligne = new Ligne();
                    ligne.Change = change;
                    ligne.Texte = Courant(items);
                    ligne.Html = change ? Rendre(items, comptes)
                        : "<span class=\"ctx\">" + Echapper(la[i]) + "</span>";
                    resultat.Add(ligne);
                }
            }
            else
            {
                // prudence (ne devrait pas arriver) : un seul diff, sans règle
                libelles["autre"] = "Corrections"; ordre.Add("autre"); comptes["autre"] = 0;
                var items = new List<Item>();
                foreach (var c in Desechapper(avant).Replace("\r\n", "\n")) items.Add(new Item(c, ' ', null));
                Composer(items, Desechapper(corrige).Replace("\r\n", "\n"), "autre");
                var ligne = new Ligne();
                ligne.Change = true; ligne.Texte = Courant(items); ligne.Html = Rendre(items, comptes);
                resultat.Add(ligne);
            }
            foreach (var cle in ordre)
            {
                if (comptes[cle] == 0) continue;
                var e = new Etape();
                e.Cle = cle; e.Libelle = libelles[cle];
                e.Texte = comptes[cle].ToString(CultureInfo.InvariantCulture);
                regles.Add(e);
            }
            return resultat;
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
        private static string Rendre(List<Item> items, Dictionary<string, int> comptes)
        {
            var sb = new StringBuilder();
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
            return sb.ToString();
        }

        // ------------------------------------------------- mise en page
        // L'essentiel du Markdown, appliqué PAR-DESSUS le diff : titres,
        // listes, citations, séparateurs centrés, gras, italiques,
        // exposants/indices, code, liens. Les marques de bloc se
        // reconnaissent sur le texte final de la ligne, puis se retirent du
        // HTML (qui peut commencer par le <span> du texte intact).
        private static string MettreEnPage(List<Ligne> lignes, string[] separateurs)
        {
            var sb = new StringBuilder();
            var change = false;
            foreach (var l in lignes) if (l.Change) change = true;
            if (!change)
                sb.Append("<p class=\"note\">Aucune correction : ce texte était déjà impeccable.</p>\n");

            var para = new List<string>();   // lignes du paragraphe en cours
            string bloc = null;               // "ul", "ol", "bq" ouvert
            var seps = new HashSet<string>(separateurs ?? new string[0]);
            Action fermerPara = delegate
            {
                if (para.Count == 0) return;
                sb.Append("<p>").Append(string.Join("<br>\n", para.ToArray())).Append("</p>\n");
                para.Clear();
            };
            Action fermerBloc = delegate
            {
                if (bloc == "ul") sb.Append("</ul>\n");
                else if (bloc == "ol") sb.Append("</ol>\n");
                else if (bloc == "bq") sb.Append("</blockquote>\n");
                bloc = null;
            };

            foreach (var l in lignes)
            {
                var t = l.Texte.TrimEnd();
                if (t.Trim().Length == 0) { fermerPara(); fermerBloc(); continue; }

                var m = Regex.Match(t, @"^(#{1,6})[ \t]+");
                if (m.Success)
                {
                    fermerPara(); fermerBloc();
                    var n = m.Groups[1].Length;
                    sb.Append("<h").Append(n).Append('>')
                      .Append(EnLigne(SansMarque(l.Html, @"#{1,6}[ \t]+")))
                      .Append("</h").Append(n).Append(">\n");
                    continue;
                }
                if (seps.Contains(t.Trim()))
                {
                    fermerPara(); fermerBloc();
                    sb.Append("<p class=\"sep\">").Append(l.Html).Append("</p>\n");
                    continue;
                }
                if (Regex.IsMatch(t.Trim(), @"^(-{3,}|\*{3,}|_{3,})$"))   // filet horizontal
                {
                    fermerPara(); fermerBloc();
                    sb.Append("<hr>\n");
                    continue;
                }
                if (Regex.IsMatch(t, @"^[-*+][ \t]+\S"))
                {
                    fermerPara();
                    if (bloc != "ul") { fermerBloc(); sb.Append("<ul>\n"); bloc = "ul"; }
                    sb.Append("<li>").Append(EnLigne(SansMarque(l.Html, @"[-*+][ \t]+"))).Append("</li>\n");
                    continue;
                }
                if (Regex.IsMatch(t, @"^\d+[.)][ \t]+\S"))
                {
                    fermerPara();
                    if (bloc != "ol") { fermerBloc(); sb.Append("<ol>\n"); bloc = "ol"; }
                    sb.Append("<li>").Append(EnLigne(SansMarque(l.Html, @"\d+[.)][ \t]+"))).Append("</li>\n");
                    continue;
                }
                if (t.StartsWith(">"))
                {
                    fermerPara();
                    if (bloc != "bq") { fermerBloc(); sb.Append("<blockquote>\n"); bloc = "bq"; }
                    sb.Append("<p>").Append(EnLigne(SansMarque(l.Html, @"&gt;[ \t]*"))).Append("</p>\n");
                    continue;
                }
                if (bloc != null) fermerBloc();
                para.Add(EnLigne(l.Html));
            }
            fermerPara(); fermerBloc();
            return sb.ToString();
        }

        // Retire la marque de bloc au début du HTML d'une ligne, qu'elle soit
        // nue ou déjà dans le <span> du texte intact.
        private static string SansMarque(string html, string marque)
        {
            return Regex.Replace(html, "^((?:<span class=\"ctx\">)?)" + marque, "$1");
        }

        // Le Markdown en ligne, par-dessus le HTML du diff (les balises
        // <ins>/<del> peuvent chevaucher : les navigateurs s'en sortent).
        private static string EnLigne(string h)
        {
            h = Regex.Replace(h, @"`([^`\n]+)`", "<code>$1</code>");
            h = Regex.Replace(h, @"\*\*([^*\n]+)\*\*", "<strong>$1</strong>");
            h = Regex.Replace(h, @"(?<![\w*])\*([^*\n]+)\*(?![\w*])", "<em>$1</em>");
            h = Regex.Replace(h, @"(?<![\w_])_([^_\n]+)_(?![\w_])", "<em>$1</em>");
            h = Regex.Replace(h, @"\^([^\^\s]+)\^", "<sup>$1</sup>");
            h = Regex.Replace(h, @"(?<!~)~([^~\s]+)~(?!~)", "<sub>$1</sub>");
            h = Regex.Replace(h, @"\[([^\]\n]+)\]\(([^)\s]+)\)", "<a href=\"$2\">$1</a>");
            return h;
        }

        // Les antislashs d'échappement du Markdown (\#, 1\., \*) sont des
        // marques de transport : indispensables à l'aller-retour .docx, mais
        // elles n'ont rien à faire dans un aperçu.
        public static string Desechapper(string texte)
        {
            return Regex.Replace(texte, @"\\([\\*_#+>~=|`\[\]!<.\-])", "$1");
        }

        private static string Echapper(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
