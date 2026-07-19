using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// Typonanny - édition Windows (.NET Framework / WinForms)
// La nounou typographique du français : un texte « au kilomètre » entre,
// un texte aux normes de l'Imprimerie nationale sort — insécables,
// guillemets, apostrophes, cadratins, ellipses… — avec aperçu
// avant/après, rapport des corrections, et comptage professionnel
// (SEC, signes sans espaces, mots, feuillets de 1 500 signes).
// Fusion des codenames « Espace vital » (spec complète dans
// EDITION-ROADMAP) et « Signe des temps ».
// Les règles se règlent dans l'interface (préréglages IN strict / souple /
// minimal + cases par règle, mémorisées dans config\typo.conf) et la liste
// des ligatures œ/æ est un simple fichier texte éditable
// (config\ligatures.txt).
// Habillage sombre de la famille Stargazer. Style compatible C# 5 pour
// compiler avec le csc.exe intégré (aucun SDK requis).

namespace Typonanny
{
    public static class Theme
    {
        public static readonly Color Nuit = Color.FromArgb(11, 16, 38);
        public static readonly Color Panneau = Color.FromArgb(19, 26, 51);
        public static readonly Color Bordure = Color.FromArgb(42, 51, 88);
        public static readonly Color Or = Color.FromArgb(212, 175, 55);
        public static readonly Color OrClair = Color.FromArgb(244, 215, 122);
        public static readonly Color Texte = Color.FromArgb(230, 230, 240);
        public static readonly Color TexteDoux = Color.FromArgb(154, 163, 192);
        public static readonly Color Ok = Color.FromArgb(152, 195, 121);
        public static readonly Color Erreur = Color.FromArgb(230, 110, 120);
        public static readonly Color Info = Color.FromArgb(122, 162, 247);

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void StyleButton(Button b, bool primary)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = primary ? Or : Bordure;
            b.FlatAppearance.BorderSize = 1;
            b.BackColor = primary ? Or : Panneau;
            b.ForeColor = primary ? Color.FromArgb(20, 20, 30) : Texte;
            b.Cursor = Cursors.Hand;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }

    // Bouton à coins arrondis (8 px), peint à la main avec anticrénelage.
    public class RoundedButton : Button
    {
        private bool _hover;

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true; Invalidate(); base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var fill = BackColor;
            if (!Enabled) fill = Theme.Mix(fill, Color.Black, 0.35f);
            else if (_hover) fill = Theme.Mix(fill, Color.White, 0.10f);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, 8))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                var border = Enabled ? FlatAppearance.BorderColor
                                     : Color.FromArgb(70, 76, 105);
                using (var p = new Pen(border)) g.DrawPath(p, path);
            }

            var tc = Enabled ? ForeColor : Color.FromArgb(120, 126, 150);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }

    // ------------------------------------------------------------ options

    // Les règles cochables. Préréglages : « in » (Imprimerie nationale
    // strict), « souple » (fine partout), « minimal » (évidences seules).
    public class OptionsTypo
    {
        public string prereglage = "in";
        public bool espaces = true;          // doubles espaces, fins de ligne
        public bool apostrophes = true;      // ' -> '
        public bool ellipses = true;         // ... -> …, etc... -> etc.
        public bool guillemets = true;       // "..." -> « ... »
        public bool tiretsDialogue = true;   // - en début de ligne -> —
        public bool intervalles = true;      // 1914-1918 -> 1914–1918
        public bool insecables = true;       // ; ! ? : « »
        public bool insecablesUnites = true; // 10 %, 10 €, 12 kg
        public bool milliers = true;         // 10 000 (fine entre groupes)
        public bool ligaturesOe = true;      // coeur -> cœur (liste blanche)
        public bool ligaturesAe = false;     // ex aequo -> ex æquo (off)
        public bool dimensions = true;       // 10 x 15 -> 10 × 15
        public bool ordinaux = true;         // 2ème -> 2e
        public bool signalerMajuscules = true; // Etat, Elève… (signalement)
        public bool signalerSeulement = false; // rapport sans correction

        // Glyphes dépendant du préréglage.
        public char AvantPonctuationHaute()   // ; ! ?
        {
            return prereglage == "souple" ? ' ' : ' ';
        }
        public char AvantDeuxPoints()
        {
            return prereglage == "souple" ? ' ' : ' ';
        }
        public char InterieurGuillemets()
        {
            return prereglage == "souple" ? ' ' : ' ';
        }
        public bool Minimal() { return prereglage == "minimal"; }
    }

    // -------------------------------------------------------------- moteur

    public class ResultatTypo
    {
        public string Texte;
        // catégorie -> nombre de corrections (ordre d'insertion conservé)
        public List<KeyValuePair<string, int>> Compteurs =
            new List<KeyValuePair<string, int>>();
        public List<string> Signalements = new List<string>();

        public void Compter(string categorie, int n)
        {
            if (n > 0) Compteurs.Add(new KeyValuePair<string, int>(categorie, n));
        }
    }

    public static class Typo
    {
        // Liste blanche des ligatures œ (formes SANS ligature -> AVEC).
        // Modifiable par l'utilisateur via config\ligatures.txt (un mot
        // ligaturé par ligne, # commente) — voir ChargerLigatures.
        public static readonly string[] LigaturesOeDefaut = new string[]
        {
            "œuf", "œufs", "bœuf", "bœufs", "cœur", "cœurs", "chœur", "chœurs",
            "sœur", "sœurs", "œuvre", "œuvres", "œuvrer", "manœuvre", "manœuvres",
            "manœuvrer", "œil", "œillet", "œillets", "œillade", "œillades",
            "œsophage", "œstrogène", "œstrogènes", "fœtus", "nœud", "nœuds",
            "vœu", "vœux", "mœurs", "œcuménique", "œcuméniques", "œnologie",
            "œnologue", "œdème", "œdèmes", "cœliaque", "Œdipe", "cœlacanthe",
            "chef-d'œuvre", "chefs-d'œuvre", "hors-d'œuvre", "main-d'œuvre"
        };
        public static readonly string[] LigaturesAeDefaut = new string[]
        {
            "ex æquo", "curriculum vitæ", "tænia", "nævus", "cæcum", "et cætera"
        };

        // ---------------------------------------------------- comptage
        // Le vocabulaire du métier : SEC (signes espaces comprises), signes
        // sans espaces, mots, feuillets de 1 500 signes, temps de lecture.

        public static string Statistiques(string texte)
        {
            if (string.IsNullOrEmpty(texte)) return "—";
            var sec = 0; var sansEspaces = 0;
            foreach (var c in texte)
            {
                if (c == '\r' || c == '\n') continue;
                sec++;
                if (!char.IsWhiteSpace(c)) sansEspaces++;
            }
            var mots = Regex.Matches(texte, @"[\p{L}\p{Nd}]+(?:['''’\-][\p{L}\p{Nd}]+)*").Count;
            var feuillets = sec / 1500.0;
            var minutes = (int)Math.Ceiling(mots / 220.0);
            return string.Format(CultureInfo.GetCultureInfo("fr-FR"),
                "SEC : {0:N0}   ·   sans espaces : {1:N0}   ·   mots : {2:N0}   ·   " +
                "feuillets (1 500) : {3:0.0}   ·   lecture : ~{4} min",
                sec, sansEspaces, mots, feuillets, minutes);
        }

        // Ventilation par chapitre si le texte a des titres Markdown (#).
        public static List<string> StatistiquesParChapitre(string texte)
        {
            var result = new List<string>();
            var lignes = texte.Replace("\r\n", "\n").Split('\n');
            string titre = null; var sb = new StringBuilder();
            foreach (var ligne in lignes)
            {
                if (Regex.IsMatch(ligne, @"^#{1,6}\s"))
                {
                    if (titre != null)
                        result.Add(titre + " — " + Statistiques(sb.ToString()));
                    titre = ligne.TrimStart('#', ' ');
                    sb.Length = 0;
                }
                else if (titre != null) sb.AppendLine(ligne);
            }
            if (titre != null)
                result.Add(titre + " — " + Statistiques(sb.ToString()));
            return result;
        }

        // ---------------------------------------------------- nettoyage

        public static ResultatTypo Nettoyer(string texte, OptionsTypo o,
            HashSet<string> ligaturesOe, HashSet<string> ligaturesAe)
        {
            var r = new ResultatTypo();

            // (1) masquer les zones protégées : URL, e-mails, chemins,
            // code Markdown, heures/ratios. Jetons en zone privée Unicode.
            var zones = new List<string>();
            var travail = Masquer(texte, zones);

            // (2) espaces bruts
            if (o.espaces)
            {
                int nFin;
                travail = RemplacerCompte(travail, @"(?m)[ \t]+(?=\r?$)", "", out nFin);
                r.Compter("espace(s) en fin de ligne", nFin);
                int nDouble;
                travail = RemplacerCompte(travail, @"(?<=[^ \n])  +(?=[^ \n])", " ", out nDouble);
                r.Compter("espace(s) en double", nDouble);
            }

            // (3) glyphes simples
            if (o.apostrophes)
            {
                var n = 0;
                var sb = new StringBuilder(travail.Length);
                foreach (var c in travail)
                {
                    if (c == '\'') { sb.Append('’'); n++; }
                    else sb.Append(c);
                }
                travail = sb.ToString();
                r.Compter("apostrophe(s) courbée(s)", n);
            }
            if (o.ellipses)
            {
                int nEtc;
                travail = RemplacerCompte(travail, @"\betc(\.\.\.+|…)", "etc.", out nEtc);
                r.Compter("« etc. » recadré(s)", nEtc);
                foreach (Match m in Regex.Matches(travail, @"\.{4,}"))
                { r.Signalements.Add("« " + m.Value + " » laissé tel quel (plus de trois points, souvent voulu)"); break; }
                int nEll;
                travail = RemplacerCompte(travail, @"(?<!\.)\.\.\.(?!\.)", "…", out nEll);
                int nEll2;
                travail = RemplacerCompte(travail, @"(?<!\.)\. \. \.(?!\.)", "…", out nEll2);
                r.Compter("points de suspension « … »", nEll + nEll2);
            }
            if (o.guillemets && !o.Minimal())
            {
                var droits = 0;
                foreach (var c in travail) if (c == '"') droits++;
                if (droits % 2 != 0)
                    r.Signalements.Add("nombre impair de guillemets droits : conversion « » non appliquée");
                else if (droits > 0)
                {
                    var nbsp = o.InterieurGuillemets();
                    var n = 0;
                    travail = Regex.Replace(travail,
                        "\"[   ]*([^\"\n]*?)[   ]*\"",
                        delegate(Match m)
                        {
                            n++;
                            return "«" + nbsp + m.Groups[1].Value + nbsp + "»";
                        });
                    r.Compter("paire(s) de guillemets « »", n);
                }
            }

            // (4) tirets
            if (o.tiretsDialogue && !o.Minimal())
            {
                int nDial;
                travail = RemplacerCompte(travail,
                    @"(?m)^([ \t]*)-{1,2}[ \t]+", "$1— ", out nDial);
                r.Compter("tiret(s) de dialogue « — »", nDial);
            }
            if (o.intervalles && !o.Minimal())
            {
                int nInt;
                travail = RemplacerCompte(travail,
                    @"(?<![\d\-–])(\d{1,4})-(\d{1,4})(?![\d\-–])", "$1–$2", out nInt);
                r.Compter("intervalle(s) en demi-cadratin", nInt);
            }

            // (5) insécables de ponctuation
            if (o.insecables && !o.Minimal())
            {
                var fine = o.AvantPonctuationHaute().ToString();
                var nbspDeux = o.AvantDeuxPoints().ToString();
                var nbsp = o.InterieurGuillemets().ToString();

                int nHaute;
                travail = RemplacerCompte(travail,
                    "(?<=[^\\s;!?:«  ])[   ]*(?=[;!?])",
                    fine, out nHaute, fine);
                r.Compter("insécable(s) fine(s) avant ; ! ?", nHaute);

                int nDeux;
                travail = RemplacerCompte(travail,
                    "(?<=[^\\s:«  ])[   ]*(?=:(?=\\s|$))",
                    nbspDeux, out nDeux, nbspDeux);
                r.Compter("insécable(s) avant « : »", nDeux);

                int nOuv;
                travail = RemplacerCompte(travail, "(?<=«)[   ]*(?=\\S)",
                    nbsp, out nOuv, nbsp);
                int nFerm;
                travail = RemplacerCompte(travail, "(?<=\\S)[   ]*(?=»)",
                    nbsp, out nFerm, nbsp);
                r.Compter("insécable(s) de guillemets", nOuv + nFerm);
            }
            if (o.insecablesUnites && !o.Minimal())
            {
                int nPc;
                travail = RemplacerCompte(travail,
                    "(?<=\\d)[   ]*(?=[%€$])", " ", out nPc, " ");
                int nUnit;
                travail = RemplacerCompte(travail,
                    "(?<=\\d)[   ]+(?=(?:kg|km|cm|mm|mn|min|ans|an|h|g|m|s|l|cl|ml|ko|mo|go|Ko|Mo|Go)\\b)",
                    " ", out nUnit, " ");
                r.Compter("insécable(s) d'unités (%, €, kg…)", nPc + nUnit);
            }
            if (o.milliers && !o.Minimal())
            {
                int nMil;
                travail = RemplacerCompte(travail,
                    "(?<=\\b\\d{1,3}) (?=\\d{3}(?!\\d))", " ", out nMil);
                r.Compter("séparateur(s) de milliers en fine", nMil);
            }

            // (6) ligatures & divers
            if (o.ligaturesOe)
            {
                var n = 0;
                travail = Ligaturer(travail, ligaturesOe, "oe", "œ", ref n);
                r.Compter("ligature(s) œ", n);
            }
            if (o.ligaturesAe)
            {
                var n = 0;
                travail = Ligaturer(travail, ligaturesAe, "ae", "æ", ref n);
                r.Compter("ligature(s) æ", n);
            }
            if (o.dimensions && !o.Minimal())
            {
                int nDim;
                travail = RemplacerCompte(travail,
                    "(?<=\\d)[   ]*[xX][   ]*(?=\\d)",
                    " × ", out nDim, " × ");
                r.Compter("dimension(s) en ×", nDim);
            }
            if (o.ordinaux)
            {
                int a, b, c, d;
                travail = RemplacerCompte(travail, @"\b1ères\b", "1res", out a);
                travail = RemplacerCompte(travail, @"\b1ère\b", "1re", out b);
                travail = RemplacerCompte(travail, @"\b2ndes?\b", "2de", out c);
                travail = RemplacerCompte(travail, @"\b(\d+)i?èmes?\b", "$1e", out d);
                r.Compter("ordinal(aux) recadré(s)", a + b + c + d);
            }
            if (o.signalerMajuscules)
            {
                foreach (Match m in Regex.Matches(travail,
                    @"(?<=^|[.!?…]\s+)(Etat|Etats|Ecole|Ecoles|Eglise|Eglises|Elève|Eleve|Ere|Ete|Etude|Etudes|Epoque|Evidemment|Egalement|A)(?=\s)",
                    RegexOptions.Multiline))
                {
                    var mot = m.Groups[1].Value;
                    var propose = mot == "A" ? "À"
                        : (mot.StartsWith("Et") || mot.StartsWith("Ec") || mot.StartsWith("Eg") ||
                           mot.StartsWith("El") || mot.StartsWith("Ep") || mot.StartsWith("Ev") ||
                           mot.StartsWith("Er"))
                            ? "É" + mot.Substring(1) : mot;
                    r.Signalements.Add("majuscule à accentuer ? « " + mot +
                        " » → « " + propose + " » (non corrigé d'office)");
                    if (r.Signalements.Count > 20) break;
                }
            }

            // (7) réinjecter les zones protégées
            r.Texte = Demasquer(travail, zones);
            return r;
        }

        // Remplace et compte les VRAIS changements (idempotence : remplacer
        // une fine par la même fine ne compte pas et ne modifie rien).
        private static string RemplacerCompte(string texte, string motif,
            string remplacement, out int n)
        {
            return RemplacerCompte(texte, motif, remplacement, out n, null);
        }

        private static string RemplacerCompte(string texte, string motif,
            string remplacement, out int n, string dejaBon)
        {
            var compte = 0;
            var rx = new Regex(motif);
            var result = rx.Replace(texte, delegate(Match m)
            {
                var produit = m.Result(remplacement);
                if (m.Value != produit &&
                    (dejaBon == null || m.Value != dejaBon)) compte++;
                return produit;
            });
            n = compte;
            return result;
        }

        // Ligatures par liste blanche : le mot ligaturé (« cœur ») donne la
        // forme à chercher (« coeur »). La casse d'origine est respectée.
        private static string Ligaturer(string texte, HashSet<string> liste,
            string digramme, string ligature, ref int n)
        {
            if (liste == null || liste.Count == 0) return texte;
            var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mot in liste)
            {
                var plat = mot.Replace(ligature, digramme)
                              .Replace(ligature.ToUpperInvariant(), digramme.ToUpperInvariant());
                table[plat] = mot;
            }
            var compte = 0;
            var result = Regex.Replace(texte,
                @"[\p{L}]*" + digramme + @"[\p{L}]*",
                delegate(Match m)
                {
                    string bon;
                    if (!table.TryGetValue(m.Value, out bon)) return m.Value;
                    compte++;
                    return RespecterCasse(m.Value, bon);
                }, RegexOptions.IgnoreCase);
            n += compte;
            return result;
        }

        private static string RespecterCasse(string original, string corrige)
        {
            var toutMaj = true;
            foreach (var c in original) if (char.IsLetter(c) && char.IsLower(c)) { toutMaj = false; break; }
            if (toutMaj && original.Length > 1) return corrige.ToUpperInvariant();
            if (char.IsUpper(original[0]) && !char.IsUpper(corrige[0]))
                return char.ToUpperInvariant(corrige[0]) + corrige.Substring(1);
            return corrige;
        }

        // ------------------------------------------- zones protégées

        private static readonly Regex[] MotifsProteges = new Regex[]
        {
            new Regex(@"```[\s\S]*?```"),                    // bloc de code md
            new Regex(@"`[^`\n]+`"),                          // span de code md
            new Regex(@"(https?|ftp)://\S+", RegexOptions.IgnoreCase),
            new Regex(@"\bwww\.\S+", RegexOptions.IgnoreCase),
            new Regex(@"[\w.+-]+@[\w-]+\.[\w.-]+"),           // e-mail
            new Regex(@"\b[A-Za-z]:\\\S+"),                   // chemin Windows
            new Regex(@"\\\\\S+"),                             // chemin UNC
            new Regex(@"\b\d{1,2}:\d{2}(?::\d{2})?\b"),       // heure 10:30
            new Regex(@"\b\d+:\d+\b"),                         // ratio 16:9
        };

        private static string Masquer(string texte, List<string> zones)
        {
            // point de départ en zone privée, au-delà de tout PUA déjà présent
            var basePua = 0xE000;
            foreach (var c in texte)
                if (c >= 0xE000 && c <= 0xF8FF && c >= basePua) basePua = c + 1;

            var travail = texte;
            foreach (var rx in MotifsProteges)
                travail = rx.Replace(travail, delegate(Match m)
                {
                    zones.Add(m.Value);
                    return ((char)(basePua + zones.Count - 1)).ToString();
                });
            return travail;
        }

        private static string Demasquer(string texte, List<string> zones)
        {
            if (zones.Count == 0) return texte;
            var basePua = -1;
            // retrouver la base : le plus petit jeton présent
            foreach (var c in texte)
                if (c >= 0xE000 && c <= 0xF8FF && (basePua < 0 || c < basePua)) basePua = c;
            if (basePua < 0) return texte;
            var sb = new StringBuilder(texte.Length + 64);
            foreach (var c in texte)
            {
                var idx = c - basePua;
                if (c >= 0xE000 && c <= 0xF8FF && idx >= 0 && idx < zones.Count)
                    sb.Append(zones[idx]);
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ------------------------------------------- config & ligatures

        public static string DossierConfig()
        {
            var baseDir = Path.GetDirectoryName(Application.ExecutablePath);
            return Path.Combine(baseDir, "config");
        }

        public static HashSet<string> ChargerLigatures(string fichier, string[] defauts)
        {
            var result = new HashSet<string>();
            try
            {
                var chemin = Path.Combine(DossierConfig(), fichier);
                if (File.Exists(chemin))
                {
                    foreach (var brute in File.ReadAllLines(chemin))
                    {
                        var ligne = brute.Trim();
                        if (ligne.Length == 0 || ligne.StartsWith("#")) continue;
                        result.Add(ligne);
                    }
                    return result;
                }
            }
            catch { }
            foreach (var mot in defauts) result.Add(mot);
            return result;
        }

        // Crée les fichiers de règles éditables s'ils manquent : la liste
        // des ligatures est à portée de Bloc-notes (typographie française
        // oblige, on prévoit des ajustements).
        public static void EcrireLigaturesParDefaut()
        {
            try
            {
                var dir = DossierConfig();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var oe = Path.Combine(dir, "ligatures.txt");
                if (!File.Exists(oe))
                    File.WriteAllLines(oe, Concat(new string[] {
                        "# Ligatures œ — un mot LIGATURÉ par ligne (les formes sans",
                        "# ligature sont déduites). « # » commente. Éditez librement,",
                        "# Typonanny relit ce fichier à chaque lancement." },
                        LigaturesOeDefaut), new UTF8Encoding(true));
                var ae = Path.Combine(dir, "ligatures-ae.txt");
                if (!File.Exists(ae))
                    File.WriteAllLines(ae, Concat(new string[] {
                        "# Ligatures æ (règle désactivée par défaut, voir Règles…)." },
                        LigaturesAeDefaut), new UTF8Encoding(true));
            }
            catch { }
        }

        private static string[] Concat(string[] a, string[] b)
        {
            var r = new string[a.Length + b.Length];
            a.CopyTo(r, 0); b.CopyTo(r, a.Length);
            return r;
        }

        // config\typo.conf : clé=valeur, lisible et éditable à la main.
        public static void SauverOptions(OptionsTypo o)
        {
            try
            {
                var dir = DossierConfig();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var lignes = new List<string>();
                lignes.Add("# Réglages Typonanny — clé=valeur (1 actif / 0 inactif).");
                lignes.Add("prereglage=" + o.prereglage);
                lignes.Add("espaces=" + (o.espaces ? "1" : "0"));
                lignes.Add("apostrophes=" + (o.apostrophes ? "1" : "0"));
                lignes.Add("ellipses=" + (o.ellipses ? "1" : "0"));
                lignes.Add("guillemets=" + (o.guillemets ? "1" : "0"));
                lignes.Add("tiretsDialogue=" + (o.tiretsDialogue ? "1" : "0"));
                lignes.Add("intervalles=" + (o.intervalles ? "1" : "0"));
                lignes.Add("insecables=" + (o.insecables ? "1" : "0"));
                lignes.Add("insecablesUnites=" + (o.insecablesUnites ? "1" : "0"));
                lignes.Add("milliers=" + (o.milliers ? "1" : "0"));
                lignes.Add("ligaturesOe=" + (o.ligaturesOe ? "1" : "0"));
                lignes.Add("ligaturesAe=" + (o.ligaturesAe ? "1" : "0"));
                lignes.Add("dimensions=" + (o.dimensions ? "1" : "0"));
                lignes.Add("ordinaux=" + (o.ordinaux ? "1" : "0"));
                lignes.Add("signalerMajuscules=" + (o.signalerMajuscules ? "1" : "0"));
                lignes.Add("signalerSeulement=" + (o.signalerSeulement ? "1" : "0"));
                File.WriteAllLines(Path.Combine(dir, "typo.conf"),
                    lignes.ToArray(), new UTF8Encoding(true));
            }
            catch { }
        }

        public static OptionsTypo ChargerOptions()
        {
            var o = new OptionsTypo();
            try
            {
                var chemin = Path.Combine(DossierConfig(), "typo.conf");
                if (!File.Exists(chemin)) return o;
                foreach (var brute in File.ReadAllLines(chemin))
                {
                    var ligne = brute.Trim();
                    if (ligne.Length == 0 || ligne.StartsWith("#")) continue;
                    var i = ligne.IndexOf('=');
                    if (i <= 0) continue;
                    var cle = ligne.Substring(0, i).Trim();
                    var val = ligne.Substring(i + 1).Trim();
                    var actif = val == "1" || val == "true";
                    switch (cle)
                    {
                        case "prereglage": o.prereglage = val; break;
                        case "espaces": o.espaces = actif; break;
                        case "apostrophes": o.apostrophes = actif; break;
                        case "ellipses": o.ellipses = actif; break;
                        case "guillemets": o.guillemets = actif; break;
                        case "tiretsDialogue": o.tiretsDialogue = actif; break;
                        case "intervalles": o.intervalles = actif; break;
                        case "insecables": o.insecables = actif; break;
                        case "insecablesUnites": o.insecablesUnites = actif; break;
                        case "milliers": o.milliers = actif; break;
                        case "ligaturesOe": o.ligaturesOe = actif; break;
                        case "ligaturesAe": o.ligaturesAe = actif; break;
                        case "dimensions": o.dimensions = actif; break;
                        case "ordinaux": o.ordinaux = actif; break;
                        case "signalerMajuscules": o.signalerMajuscules = actif; break;
                        case "signalerSeulement": o.signalerSeulement = actif; break;
                    }
                }
            }
            catch { }
            return o;
        }
    }

    // ------------------------------------------------ fenêtre des règles

    public class ReglesDialog : Form
    {
        private readonly RadioButton _in, _souple, _minimal;
        private readonly CheckBox[] _cases;
        private readonly string[] _cles;
        private readonly CheckBox _signaler;
        public OptionsTypo Resultat;

        public ReglesDialog(OptionsTypo o)
        {
            Text = "Règles typographiques — Typonanny";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(500, 520);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Nuit; ForeColor = Theme.Texte;
            Font = new Font("Segoe UI", 9f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            var titrePre = new Label();
            titrePre.Text = "Préréglage";
            titrePre.Font = new Font("Segoe UI Semibold", 10f);
            titrePre.ForeColor = Theme.OrClair;
            titrePre.SetBounds(16, 12, 200, 20);

            _in = Radio("Imprimerie nationale (strict) — fine avant ; ! ?, pleine avant : et dans « »", 36);
            _souple = Radio("Souple / maison — fine insécable partout", 60);
            _minimal = Radio("Minimal — évidences seules (apostrophes, …, espaces)", 84);
            if (o.prereglage == "souple") _souple.Checked = true;
            else if (o.prereglage == "minimal") _minimal.Checked = true;
            else _in.Checked = true;

            var titreRegles = new Label();
            titreRegles.Text = "Règles (le préréglage Minimal ignore celles marquées °)";
            titreRegles.Font = new Font("Segoe UI Semibold", 10f);
            titreRegles.ForeColor = Theme.OrClair;
            titreRegles.SetBounds(16, 116, 460, 20);

            var libelles = new string[] {
                "Espaces (doubles, fins de ligne)",
                "Apostrophes courbes '",
                "Points de suspension … et « etc. »",
                "Guillemets français « » °",
                "Tirets de dialogue — °",
                "Intervalles 1914–1918 °",
                "Insécables de ponctuation ; ! ? : « » °",
                "Insécables d'unités 10 %, 10 €, 12 kg °",
                "Milliers en fine 10 000 °",
                "Ligatures œ (liste : config\\ligatures.txt)",
                "Ligatures æ (config\\ligatures-ae.txt)",
                "Dimensions 10 × 15 °",
                "Ordinaux 2ème → 2e",
                "Signaler les majuscules à accentuer (État, À…)"
            };
            _cles = new string[] { "espaces", "apostrophes", "ellipses", "guillemets",
                "tiretsDialogue", "intervalles", "insecables", "insecablesUnites",
                "milliers", "ligaturesOe", "ligaturesAe", "dimensions", "ordinaux",
                "signalerMajuscules" };
            var valeurs = new bool[] { o.espaces, o.apostrophes, o.ellipses, o.guillemets,
                o.tiretsDialogue, o.intervalles, o.insecables, o.insecablesUnites,
                o.milliers, o.ligaturesOe, o.ligaturesAe, o.dimensions, o.ordinaux,
                o.signalerMajuscules };

            _cases = new CheckBox[libelles.Length];
            for (var i = 0; i < libelles.Length; i++)
            {
                var chk = new CheckBox();
                chk.Text = libelles[i];
                chk.Checked = valeurs[i];
                chk.ForeColor = Theme.Texte;
                var col = i % 2; var lg = i / 2;
                chk.SetBounds(16 + col * 240, 140 + lg * 26, 236, 24);
                _cases[i] = chk;
                Controls.Add(chk);
            }

            _signaler = new CheckBox();
            _signaler.Text = "Signaler seulement : rapport complet, aucune correction écrite";
            _signaler.Checked = o.signalerSeulement;
            _signaler.ForeColor = Theme.OrClair;
            _signaler.SetBounds(16, 336, 460, 24);

            var note = new Label();
            note.Text = "Les listes de ligatures sont de simples fichiers texte dans " +
                "config\\ — éditez-les au Bloc-notes, Typonanny les relit à chaque " +
                "lancement. Le reste des réglages vit dans config\\typo.conf.";
            note.ForeColor = Theme.TexteDoux;
            note.SetBounds(16, 372, 460, 56);

            var ok = new RoundedButton();
            ok.Text = "Enregistrer";
            ok.SetBounds(286, 474, 100, 30);
            Theme.StyleButton(ok, true);
            ok.Click += delegate(object s, EventArgs e) { Valider(); };

            var cancel = new RoundedButton();
            cancel.Text = "Annuler";
            cancel.SetBounds(394, 474, 90, 30);
            Theme.StyleButton(cancel, false);
            cancel.Click += delegate(object s, EventArgs e) { DialogResult = DialogResult.Cancel; };

            Controls.Add(titrePre); Controls.Add(_in); Controls.Add(_souple);
            Controls.Add(_minimal); Controls.Add(titreRegles);
            Controls.Add(_signaler); Controls.Add(note);
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }

        private RadioButton Radio(string texte, int y)
        {
            var r = new RadioButton();
            r.Text = texte;
            r.ForeColor = Theme.Texte;
            r.SetBounds(16, y, 470, 22);
            return r;
        }

        private void Valider()
        {
            var o = new OptionsTypo();
            o.prereglage = _souple.Checked ? "souple" : (_minimal.Checked ? "minimal" : "in");
            var valeurs = new bool[_cases.Length];
            for (var i = 0; i < _cases.Length; i++) valeurs[i] = _cases[i].Checked;
            o.espaces = valeurs[0]; o.apostrophes = valeurs[1]; o.ellipses = valeurs[2];
            o.guillemets = valeurs[3]; o.tiretsDialogue = valeurs[4]; o.intervalles = valeurs[5];
            o.insecables = valeurs[6]; o.insecablesUnites = valeurs[7]; o.milliers = valeurs[8];
            o.ligaturesOe = valeurs[9]; o.ligaturesAe = valeurs[10]; o.dimensions = valeurs[11];
            o.ordinaux = valeurs[12]; o.signalerMajuscules = valeurs[13];
            o.signalerSeulement = _signaler.Checked;
            Resultat = o;
            DialogResult = DialogResult.OK;
        }
    }

    // ------------------------------------------------------------- fenêtre

    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr,
            ref int value, int size);

        private readonly TextBox _avant;
        private readonly TextBox _apres;
        private readonly Label _stats;
        private readonly ListBox _rapport;
        private readonly Label _status;
        private OptionsTypo _options;
        private string _fichierSource;   // pour « Enregistrer sous » et le BOM
        private bool _bomSource = true;

        public MainForm(string fichierInitial)
        {
            Text = "Typonanny";
            ClientSize = new Size(980, 640);
            MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            BackColor = Theme.Nuit;
            ForeColor = Theme.Texte;
            AllowDrop = true;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            Typo.EcrireLigaturesParDefaut();
            _options = Typo.ChargerOptions();

            var titre = new Label();
            titre.Text = "La nounou de votre typographie française.";
            titre.Font = new Font("Segoe UI Semibold", 12f);
            titre.ForeColor = Theme.OrClair;
            titre.SetBounds(20, 14, 500, 24);

            var ouvrir = Bouton("Ouvrir un texte…", 20, 46, 130, false);
            ouvrir.Click += delegate(object s, EventArgs e) { Ouvrir(); };
            var coller = Bouton("Coller", 158, 46, 76, false);
            coller.Click += delegate(object s, EventArgs e)
            {
                try { if (Clipboard.ContainsText()) { _avant.Text = Clipboard.GetText(); _fichierSource = null; } }
                catch { }
            };
            var nettoyer = Bouton("✨  Nettoyer", 242, 46, 120, true);
            nettoyer.Click += delegate(object s, EventArgs e) { Nettoyer(); };
            var copier = Bouton("Copier le résultat", 370, 46, 130, false);
            copier.Click += delegate(object s, EventArgs e)
            {
                try { if (_apres.Text.Length > 0) Clipboard.SetText(_apres.Text); } catch { }
            };
            var enregistrer = Bouton("Enregistrer sous…", 508, 46, 130, false);
            enregistrer.Click += delegate(object s, EventArgs e) { Enregistrer(); };
            var regles = Bouton("Règles…", 646, 46, 90, false);
            regles.Click += delegate(object s, EventArgs e) { OuvrirRegles(); };

            var lAvant = new Label();
            lAvant.Text = "Avant (collez ou déposez votre texte) :";
            lAvant.ForeColor = Theme.TexteDoux;
            lAvant.SetBounds(20, 88, 400, 18);

            var lApres = new Label();
            lApres.Text = "Après :";
            lApres.ForeColor = Theme.TexteDoux;
            lApres.SetBounds(0, 88, 200, 18);   // repositionné au resize

            _avant = ZoneTexte();
            _apres = ZoneTexte();
            _apres.ReadOnly = true;
            _avant.TextChanged += delegate(object s, EventArgs e)
            {
                _stats.Text = Typo.Statistiques(_avant.Text);
            };

            _stats = new Label();
            _stats.Text = "—";
            _stats.ForeColor = Theme.OrClair;
            _stats.Font = new Font("Segoe UI", 9f);
            _stats.AutoEllipsis = true;

            _rapport = new ListBox();
            _rapport.BackColor = Theme.Panneau;
            _rapport.ForeColor = Theme.Texte;
            _rapport.BorderStyle = BorderStyle.FixedSingle;
            _rapport.IntegralHeight = false;
            _rapport.HorizontalScrollbar = true;

            _status = new Label();
            _status.ForeColor = Theme.TexteDoux;
            _status.AutoEllipsis = true;

            Controls.Add(titre);
            Controls.Add(lAvant); Controls.Add(lApres);
            Controls.Add(_avant); Controls.Add(_apres);
            Controls.Add(_stats); Controls.Add(_rapport); Controls.Add(_status);

            Resize += delegate(object s, EventArgs e) { Disposer(lApres); };
            Disposer(lApres);

            DragEnter += delegate(object s, DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop)
                    ? DragDropEffects.Copy : DragDropEffects.None;
            };
            DragDrop += delegate(object s, DragEventArgs e)
            {
                var fichiers = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (fichiers != null && fichiers.Length > 0) ChargerFichier(fichiers[0]);
            };

            if (fichierInitial != null) ChargerFichier(fichierInitial);
        }

        private RoundedButton Bouton(string texte, int x, int y, int largeur, bool primaire)
        {
            var b = new RoundedButton();
            b.Text = texte;
            b.SetBounds(x, y, largeur, 30);
            Theme.StyleButton(b, primaire);
            Controls.Add(b);
            return b;
        }

        private TextBox ZoneTexte()
        {
            var t = new TextBox();
            t.Multiline = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.BackColor = Theme.Panneau;
            t.ForeColor = Theme.Texte;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = new Font("Segoe UI", 10f);
            t.AcceptsReturn = true;
            return t;
        }

        // Mise en page manuelle : deux colonnes égales, stats + rapport en bas.
        private void Disposer(Label lApres)
        {
            var largeur = (ClientSize.Width - 60) / 2;
            var hautZones = 110;
            var hautRapport = 96;
            var hauteur = ClientSize.Height - hautZones - hautRapport - 66;
            _avant.SetBounds(20, hautZones, largeur, hauteur);
            _apres.SetBounds(40 + largeur, hautZones, largeur, hauteur);
            lApres.Left = 40 + largeur;
            lApres.Top = 88;
            _stats.SetBounds(20, hautZones + hauteur + 8, ClientSize.Width - 40, 20);
            _rapport.SetBounds(20, hautZones + hauteur + 32, ClientSize.Width - 40, hautRapport - 12);
            _status.SetBounds(20, ClientSize.Height - 26, ClientSize.Width - 40, 20);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, 4);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); return; }
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                Nettoyer();
                e.Handled = true; e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        private void Ouvrir()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Choisissez le texte à confier à la nounou";
                dlg.Filter = "Textes (*.txt;*.md)|*.txt;*.md|Tous les fichiers|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ChargerFichier(dlg.FileName);
            }
        }

        private void ChargerFichier(string chemin)
        {
            try
            {
                var octets = File.ReadAllBytes(chemin);
                _bomSource = octets.Length >= 3 && octets[0] == 0xEF &&
                             octets[1] == 0xBB && octets[2] == 0xBF;
                _avant.Text = File.ReadAllText(chemin);
                _fichierSource = chemin;
                _status.Text = chemin;
                _apres.Text = "";
                _rapport.Items.Clear();
            }
            catch (Exception ex)
            {
                _status.Text = "Impossible de lire le fichier : " + ex.Message;
                _status.ForeColor = Theme.Erreur;
            }
        }

        private void Nettoyer()
        {
            if (_avant.Text.Length == 0)
            {
                _status.Text = "Rien à nettoyer : collez ou ouvrez un texte d'abord.";
                _status.ForeColor = Theme.TexteDoux;
                return;
            }
            var oe = Typo.ChargerLigatures("ligatures.txt", Typo.LigaturesOeDefaut);
            var ae = Typo.ChargerLigatures("ligatures-ae.txt", Typo.LigaturesAeDefaut);
            var r = Typo.Nettoyer(_avant.Text, _options, oe, ae);
            _apres.Text = _options.signalerSeulement ? _avant.Text : r.Texte;

            _rapport.Items.Clear();
            var total = 0;
            foreach (var kv in r.Compteurs)
            {
                total += kv.Value;
                _rapport.Items.Add("✔  " + kv.Value + " " + kv.Key);
            }
            foreach (var s in r.Signalements) _rapport.Items.Add("⚠  " + s);
            if (total == 0 && r.Signalements.Count == 0)
                _rapport.Items.Add("Rien à redire : ce texte est déjà impeccable. Étonnant.");

            foreach (var ligne in Typo.StatistiquesParChapitre(_avant.Text))
                _rapport.Items.Add("§  " + ligne);

            _status.Text = _options.signalerSeulement
                ? total + " correction(s) possibles (mode signalement : rien n'a été modifié)."
                : total + " correction(s) appliquée(s)" +
                  (r.Signalements.Count > 0 ? ", " + r.Signalements.Count + " signalement(s)." : ".");
            _status.ForeColor = Theme.Ok;
        }

        private void Enregistrer()
        {
            if (_apres.Text.Length == 0)
            {
                _status.Text = "Nettoyez d'abord : c'est le résultat qui s'enregistre.";
                _status.ForeColor = Theme.TexteDoux;
                return;
            }
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Enregistrer le texte nettoyé";
                if (_fichierSource != null)
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(_fichierSource);
                    dlg.FileName = Path.GetFileNameWithoutExtension(_fichierSource) +
                        "-typo" + Path.GetExtension(_fichierSource);
                }
                else dlg.FileName = "texte-typo.txt";
                dlg.Filter = "Texte (*.txt)|*.txt|Markdown (*.md)|*.md|Tous les fichiers|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    // jamais l'original : le dialogue propose déjà « -typo »
                    File.WriteAllText(dlg.FileName, _apres.Text,
                        new UTF8Encoding(_bomSource));
                    _status.Text = "Enregistré : " + dlg.FileName;
                    _status.ForeColor = Theme.Ok;
                }
                catch (Exception ex)
                {
                    _status.Text = "Impossible d'enregistrer : " + ex.Message;
                    _status.ForeColor = Theme.Erreur;
                }
            }
        }

        private void OuvrirRegles()
        {
            using (var dlg = new ReglesDialog(_options))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _options = dlg.Resultat;
                    Typo.SauverOptions(_options);
                }
            }
        }
    }

    // ---------------------------------------------------------- démarrage

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var fichier = args.Length > 0 && File.Exists(args[0]) ? args[0] : null;
            Application.Run(new MainForm(fichier));
        }
    }
}
