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

// Typo.cs — le moteur : les règles typographiques (OptionsTypo), leur
// résultat (ResultatTypo) et Typo, la nounou elle-même (nettoyage,
// signalements, comptage du métier, réglages et listes de ligatures).

namespace Typonanny
{
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
        public bool protegerSeparateurs = true; // les séparateurs de texte restent tels quels
        public string separateurs = "***";      // « *** ~ » : un par mot, séparés par des espaces

        // Les séparateurs à protéger, un par entrée (vide si l'option est off).
        public string[] Separateurs()
        {
            if (!protegerSeparateurs || separateurs == null) return new string[0];
            var l = new List<string>();
            foreach (var s in separateurs.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                if (!l.Contains(s)) l.Add(s);
            return l.ToArray();
        }

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
            var travail = Masquer(texte, zones, o.Separateurs());

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
                // « \- » : Pandoc échappe un tiret en début de ligne (sinon
                // ce serait une liste) ; le cadratin, lui, n'a pas besoin
                // d'échappement — l'antislash part avec la correction.
                travail = RemplacerCompte(travail,
                    @"(?m)^([ \t]*)\\?-{1,2}[ \t]+", "$1— ", out nDial);
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

        // Une ligne qui n'est qu'un séparateur de texte (« *** »), avec ou
        // sans les antislashs d'échappement de Pandoc (« \*\*\* »).
        private static Regex MotifSeparateur(string sep)
        {
            var sb = new StringBuilder(@"(?m)^[ \t]*");
            foreach (var c in sep) sb.Append(@"\\?").Append(Regex.Escape(c.ToString()));
            sb.Append(@"[ \t]*(?=\r?$)");
            return new Regex(sb.ToString());
        }

        private static string Masquer(string texte, List<string> zones, string[] separateurs)
        {
            // point de départ en zone privée, au-delà de tout PUA déjà présent
            var basePua = 0xE000;
            foreach (var c in texte)
                if (c >= 0xE000 && c <= 0xF8FF && c >= basePua) basePua = c + 1;

            var motifs = new List<Regex>(MotifsProteges);
            foreach (var s in separateurs) motifs.Add(MotifSeparateur(s));
            var travail = texte;
            foreach (var rx in motifs)
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
                lignes.Add("protegerSeparateurs=" + (o.protegerSeparateurs ? "1" : "0"));
                lignes.Add("separateurs=" + (o.separateurs ?? ""));
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
                        case "protegerSeparateurs": o.protegerSeparateurs = actif; break;
                        case "separateurs": o.separateurs = val; break;
                    }
                }
            }
            catch { }
            return o;
        }
    }

}
