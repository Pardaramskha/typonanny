using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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

    // ------------------------------------------- documents (.docx / .odt)
    // Le pont vers les manuscrits mis en forme. Deux niveaux :
    // 1. Pandoc disponible (dossier de dépendances partagé du hub, installé
    //    d'un clic depuis Skadoosh) : aller-retour complet — le document
    //    entre en Markdown éditable, ressort dans son format d'origine,
    //    mise en forme (gras, titres, listes…) conservée.
    // 2. Sans Pandoc : extraction native du texte brut depuis le XML
    //    (word/document.xml, content.xml) — lecture seule, sortie .txt/.md.

    public static class PontDocuments
    {
        // Recherche : dossier partagé (<hub>\dependencies) → bin\ local → PATH.
        public static string TrouverPandoc(string appDir)
        {
            try
            {
                var hub = Path.GetDirectoryName(Path.GetDirectoryName(appDir));
                if (hub != null && File.Exists(Path.Combine(hub, "Stargazer.exe")))
                {
                    var partage = Path.Combine(Path.Combine(hub, "dependencies"), "pandoc.exe");
                    if (File.Exists(partage)) return partage;
                }
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
        public static string ImporterEnMarkdown(string pandoc, string document)
        {
            var temp = Path.Combine(Path.GetTempPath(),
                "typonanny_" + Guid.NewGuid().ToString("N") + ".md");
            try
            {
                Executer(pandoc, "--wrap=none -t markdown -o \"" + temp +
                    "\" \"" + document + "\"");
                return File.ReadAllText(temp);
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }

        // Markdown nettoyé -> document (.docx ou .odt selon l'extension de
        // la destination). Les insécables sont de simples caractères Unicode :
        // elles survivent à l'aller-retour. « referenceDoc » (optionnel) :
        // le document d'origine sert de gabarit de styles (--reference-doc),
        // pour que polices et titres restent ceux de la maison.
        public static void ExporterDepuisMarkdown(string pandoc, string markdown,
            string dest)
        {
            ExporterDepuisMarkdown(pandoc, markdown, dest, null);
        }

        public static void ExporterDepuisMarkdown(string pandoc, string markdown,
            string dest, string referenceDoc)
        {
            var temp = Path.Combine(Path.GetTempPath(),
                "typonanny_" + Guid.NewGuid().ToString("N") + ".md");
            try
            {
                File.WriteAllText(temp, markdown, new UTF8Encoding(false));
                var gabarit = referenceDoc != null && File.Exists(referenceDoc)
                    ? "--reference-doc=\"" + referenceDoc + "\" " : "";
                Executer(pandoc, "--standalone -f markdown " + gabarit +
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

        private class OpCaractere
        {
            public char Type;       // ' ' inchangé, '-' supprimé, '+' ajouté
            public char Caractere;
            public OpCaractere(char type, char c) { Type = type; Caractere = c; }
        }

        // Diff de caractères par Myers (même algorithme que One di-version,
        // au caractère près). Les corrections typo sont locales : d reste
        // minuscule, c'est quasi instantané. null si ça diverge (garde-fou).
        private static List<OpCaractere> DiffCaracteres(string a, string b)
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
        public static string MarkdownVersHtml(string pandoc, string markdown)
        {
            var tempMd = Path.Combine(Path.GetTempPath(),
                "typonanny_" + Guid.NewGuid().ToString("N") + ".md");
            var tempHtml = Path.ChangeExtension(tempMd, ".html");
            try
            {
                File.WriteAllText(tempMd, markdown, new UTF8Encoding(false));
                // -raw_html : un manuscrit est du texte — une balise <script>
                // qui traîne doit s'afficher, pas s'exécuter dans l'aperçu.
                Executer(pandoc, "--wrap=none -f markdown-raw_html -t html -o \"" +
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

    // --------------------------------------------- aperçu (navigateur)
    // Le résultat se relit dans le navigateur, mis en page : gras,
    // italiques, exposants et compagnie rendus proprement, cadratins et
    // insécables affichés tels quels (UTF-8). Pandoc fait le rendu quand
    // il est là ; sinon, un mini-rendu Markdown maison prend le relais.

    public static class Apercu
    {
        public static string Construire(string nomDocument, string texte, string pandoc)
        {
            string corps = null;
            if (pandoc != null)
                try { corps = PontDocuments.MarkdownVersHtml(pandoc, texte); }
                catch { }
            if (corps == null) corps = RenduSommaire(texte);

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
            sb.Append("main{max-width:44em;margin:2.5em auto;padding:0 1.5em;");
            sb.Append("font-family:Georgia,'Times New Roman',serif;font-size:1.05em;");
            sb.Append("line-height:1.75}\n");
            sb.Append("main h1,main h2,main h3,main h4{color:#f4d77a;");
            sb.Append("font-family:'Segoe UI',sans-serif;line-height:1.3}\n");
            sb.Append("main a{color:#7aa2f7}\n");
            sb.Append("blockquote{border-left:3px solid #d4af37;margin-left:0;");
            sb.Append("padding-left:1em;color:#9aa3c0}\n");
            sb.Append("code{background:#131a33;padding:.1em .3em;border-radius:4px}\n");
            sb.Append("</style>\n</head>\n<body>\n<header>\n");
            sb.Append("<h1>⭐ Stargazer — Typonanny</h1>\n");
            sb.Append("<p>").Append(Echapper(nomDocument)).Append("</p>\n");
            sb.Append("</header>\n<main>\n");
            sb.Append(corps);
            sb.Append("\n</main>\n</body>\n</html>\n");
            return sb.ToString();
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
        private string _resultat;   // texte nettoyé (aperçu, copie, export)
        private readonly Label _stats;
        private readonly ListBox _rapport;
        private readonly Label _status;
        private OptionsTypo _options;
        private string _fichierSource;   // pour « Enregistrer sous » et le BOM
        private bool _bomSource = true;
        private string _formatSource;    // "docx"/"odt" si importé via Pandoc
        private string _texteImporte;    // tel qu'importé, pour détecter les retouches
        private readonly string _appDir =
            Path.GetDirectoryName(Application.ExecutablePath);

        public MainForm(string fichierInitial)
        {
            Text = "Typonanny";
            ClientSize = new Size(980, 640);
            MinimumSize = new Size(900, 520);
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
            var apercu = Bouton("👁  Ouvrir l'aperçu", 370, 46, 140, false);
            apercu.Click += delegate(object s, EventArgs e) { OuvrirApercu(); };
            var copier = Bouton("Copier", 518, 46, 80, false);
            copier.Click += delegate(object s, EventArgs e)
            {
                try { if (_resultat != null && _resultat.Length > 0) Clipboard.SetText(_resultat); }
                catch { }
            };
            var enregistrer = Bouton("Enregistrer sous…", 606, 46, 140, false);
            enregistrer.Click += delegate(object s, EventArgs e) { Enregistrer(); };
            var regles = Bouton("Règles…", 754, 46, 90, false);
            regles.Click += delegate(object s, EventArgs e) { OuvrirRegles(); };

            var lAvant = new Label();
            lAvant.Text = "Votre texte (collez, déposez ou ouvrez) — le résultat se " +
                "relit via « Ouvrir l'aperçu », mis en page dans le navigateur :";
            lAvant.ForeColor = Theme.TexteDoux;
            lAvant.SetBounds(20, 88, 800, 18);

            _avant = ZoneTexte();
            _avant.TextChanged += delegate(object s, EventArgs e)
            {
                _stats.Text = Typo.Statistiques(_avant.Text);
                _resultat = null;   // le texte a bougé : l'ancien résultat est périmé
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
            Controls.Add(lAvant);
            Controls.Add(_avant);
            Controls.Add(_stats); Controls.Add(_rapport); Controls.Add(_status);

            Resize += delegate(object s, EventArgs e) { Disposer(); };
            Disposer();

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
            t.MaxLength = 0;   // sans ça, WinForms tronque à 32 767 caractères
            return t;
        }

        // Mise en page manuelle : le texte pleine largeur (l'aperçu vit dans
        // le navigateur), stats + rapport en bas.
        private void Disposer()
        {
            var hautZones = 110;
            var hautRapport = 96;
            var hauteur = ClientSize.Height - hautZones - hautRapport - 66;
            _avant.SetBounds(20, hautZones, ClientSize.Width - 40, hauteur);
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
                dlg.Filter = "Textes et documents (*.txt;*.md;*.docx;*.odt)|" +
                    "*.txt;*.md;*.docx;*.odt|Tous les fichiers|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ChargerFichier(dlg.FileName);
            }
        }

        private void ChargerFichier(string chemin)
        {
            try
            {
                var ext = Path.GetExtension(chemin).ToLowerInvariant();
                if (ext == ".docx" || ext == ".odt")
                {
                    // Manuscrit mis en forme : Pandoc si possible (aller-
                    // retour complet), sinon texte brut extrait du XML.
                    var pandoc = PontDocuments.TrouverPandoc(_appDir);
                    if (pandoc != null)
                    {
                        _avant.Text = PontDocuments.ImporterEnMarkdown(pandoc, chemin);
                        _formatSource = ext.TrimStart('.');
                        _status.Text = chemin + " — importé via Pandoc : " +
                            "l'enregistrement redonnera un ." + _formatSource +
                            " avec ses styles d'origine.";
                        _status.ForeColor = Theme.Ok;
                    }
                    else
                    {
                        _avant.Text = PontDocuments.ExtraireTexteBrut(chemin);
                        _formatSource = ext == ".docx" ? "docx" : null;
                        _status.Text = chemin + " — texte extrait sans mise en " +
                            "forme (installez Pandoc via Skadoosh > Installer les " +
                            "dépendances pour l'aperçu structuré)." +
                            (ext == ".docx" ? " L'enregistrement en .docx garde " +
                             "quand même les styles (correction directe du document)." : "");
                        _status.ForeColor = Theme.Info;
                    }
                    _texteImporte = _avant.Text;
                    _bomSource = true;
                }
                else
                {
                    var octets = File.ReadAllBytes(chemin);
                    _bomSource = octets.Length >= 3 && octets[0] == 0xEF &&
                                 octets[1] == 0xBB && octets[2] == 0xBF;
                    _avant.Text = File.ReadAllText(chemin);
                    _formatSource = null;
                    _texteImporte = null;
                    _status.Text = chemin;
                    _status.ForeColor = Theme.TexteDoux;
                }
                _fichierSource = chemin;
                _resultat = null;
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
            _resultat = _options.signalerSeulement ? _avant.Text : r.Texte;

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

            _status.Text = (_options.signalerSeulement
                ? total + " correction(s) possibles (mode signalement : rien n'a été modifié)."
                : total + " correction(s) appliquée(s)" +
                  (r.Signalements.Count > 0 ? ", " + r.Signalements.Count + " signalement(s)." : ".")) +
                "  « Ouvrir l'aperçu » pour relire confortablement.";
            _status.ForeColor = Theme.Ok;
        }

        // Écrit l'aperçu HTML dans exports\apercu.html (écrasé à chaque
        // fois) et l'ouvre dans le navigateur par défaut.
        private void OuvrirApercu()
        {
            if (_resultat == null)
            {
                Nettoyer();   // par confort : nettoie puis montre
                if (_resultat == null) return;
            }
            try
            {
                var dir = Path.Combine(_appDir, "exports");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var chemin = Path.Combine(dir, "apercu.html");
                var nomDoc = _fichierSource != null
                    ? Path.GetFileName(_fichierSource) : "texte collé";
                var pandoc = PontDocuments.TrouverPandoc(_appDir);
                File.WriteAllText(chemin,
                    Apercu.Construire(nomDoc, _resultat, pandoc),
                    new UTF8Encoding(true));
                System.Diagnostics.Process.Start(chemin);
                _status.Text = "Aperçu ouvert dans le navigateur — " + chemin;
                _status.ForeColor = Theme.Ok;
            }
            catch (Exception ex)
            {
                _status.Text = "Impossible d'ouvrir l'aperçu : " + ex.Message;
                _status.ForeColor = Theme.Erreur;
            }
        }

        private void Enregistrer()
        {
            if (_resultat == null || _resultat.Length == 0)
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
                if (_formatSource == "docx")
                    dlg.Filter = "Document Word (*.docx)|*.docx|" +
                        "Document OpenDocument (*.odt)|*.odt|" +
                        "Texte (*.txt)|*.txt|Markdown (*.md)|*.md|Tous les fichiers|*.*";
                else if (_formatSource == "odt")
                    dlg.Filter = "Document OpenDocument (*.odt)|*.odt|" +
                        "Document Word (*.docx)|*.docx|" +
                        "Texte (*.txt)|*.txt|Markdown (*.md)|*.md|Tous les fichiers|*.*";
                else
                    dlg.Filter = "Texte (*.txt)|*.txt|Markdown (*.md)|*.md|Tous les fichiers|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    // jamais l'original : le dialogue propose déjà « -typo »
                    var extOut = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    var memeFormat = _formatSource != null &&
                        extOut == "." + _formatSource &&
                        _fichierSource != null && File.Exists(_fichierSource);
                    var texteIntact = _texteImporte != null &&
                        _avant.Text == _texteImporte;

                    if (extOut == ".docx" && memeFormat && texteIntact &&
                        !_options.signalerSeulement)
                    {
                        // Chemin royal : correction directe du document
                        // d'origine — gras, couleurs, polices, styles, tout
                        // reste exactement en place.
                        var oe = Typo.ChargerLigatures("ligatures.txt",
                            Typo.LigaturesOeDefaut);
                        var ae = Typo.ChargerLigatures("ligatures-ae.txt",
                            Typo.LigaturesAeDefaut);
                        var n = PontDocuments.ChirurgieDocx(_fichierSource,
                            dlg.FileName, _options, oe, ae);
                        _status.Text = "Enregistré : " + dlg.FileName + " — " + n +
                            " correction(s), styles d'origine conservés à l'identique.";
                        _status.ForeColor = Theme.Ok;
                        return;
                    }
                    if (extOut == ".docx" || extOut == ".odt")
                    {
                        var pandoc = PontDocuments.TrouverPandoc(_appDir);
                        if (pandoc == null)
                            throw new Exception("Pandoc introuvable — enregistrez " +
                                "en .txt/.md, ou installez Pandoc via Skadoosh.");
                        // le document d'origine sert de gabarit de styles
                        PontDocuments.ExporterDepuisMarkdown(pandoc, _resultat,
                            dlg.FileName, memeFormat ? _fichierSource : null);
                        _status.Text = "Enregistré : " + dlg.FileName +
                            (memeFormat && !texteIntact
                                ? " — retouches manuelles incluses (mise en forme " +
                                  "reconstruite depuis le texte, styles du document " +
                                  "appliqués)."
                                : ".");
                        _status.ForeColor = Theme.Ok;
                        return;
                    }
                    File.WriteAllText(dlg.FileName, _resultat,
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
