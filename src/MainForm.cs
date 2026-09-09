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

// MainForm.cs — la fenêtre principale : le texte pleine largeur, la barre
// de boutons, les statistiques, le rapport et le statut.

namespace Typonanny
{
    // ------------------------------------------------------------- fenêtre

    public class MainForm : Form
    {
        private readonly RoundedField _avant;
        private string _resultat;      // ce que Copier/Enregistrer produisent
        private string _avantNettoye;  // le texte tel qu'au dernier nettoyage
        private string _corrige;       // le texte corrigé (même en signalement)
        private List<Etape> _etapes = new List<Etape>();   // le texte après chaque règle
        private readonly Label _lAvant;   // l'intitulé au-dessus du texte (note d'import)
        private ToolStripMenuItem _aide;
        private ToolStripMenuItem _verifierMaj;
        private Updater.Info _maj;        // la mise à jour trouvée au lancement, s'il y en a une
        private readonly Label _stats;
        private readonly RoundedList _rapport;
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
            ClientSize = new Size(980, 666);
            MinimumSize = new Size(940, 520);
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

            // la barre de menus : Fichier, Aide (mises à jour, à propos)
            var menu = new MenuStrip();
            var fichier = new ToolStripMenuItem("Fichier");
            fichier.DropDownItems.Add(Menus.Entree("Ouvrir un texte…", Keys.Control | Keys.O,
                delegate { Ouvrir(); }));
            fichier.DropDownItems.Add(Menus.Entree("Enregistrer sous…", Keys.Control | Keys.S,
                delegate { Enregistrer(); }));
            fichier.DropDownItems.Add(Menus.Entree("Règles…", Keys.Control | Keys.R,
                delegate { OuvrirRegles(); }));
            fichier.DropDownItems.Add(new ToolStripSeparator());
            fichier.DropDownItems.Add(Menus.Entree("Quitter", Keys.None, delegate { Close(); }));
            _aide = new ToolStripMenuItem("Aide");
            _verifierMaj = Menus.Entree("Vérifier les mises à jour…", Keys.None,
                delegate { VerifierMisesAJour(); });
            _aide.DropDownItems.Add(_verifierMaj);
            _aide.DropDownItems.Add(new ToolStripSeparator());
            _aide.DropDownItems.Add(Menus.Entree("À propos de Typonanny", Keys.None,
                delegate { APropos(); }));
            menu.Items.Add(fichier);
            menu.Items.Add(_aide);
            Menus.Styler(menu);
            MainMenuStrip = menu;
            Controls.Add(menu);

            var titre = new Label();
            titre.Text = "La nounou de votre typographie française.";
            titre.Font = new Font("Segoe UI Semibold", 12f);
            titre.ForeColor = Theme.OrClair;
            titre.SetBounds(20, 40, 500, 24);

            // la barre de boutons : icône + texte, de gauche à droite
            var x = 20;
            var ouvrir = Bouton("Ouvrir un texte…", "ouvrir", ref x, 150, false);
            ouvrir.Click += delegate(object s, EventArgs e) { Ouvrir(); };
            var coller = Bouton("Coller", "coller", ref x, 96, false);
            coller.Click += delegate(object s, EventArgs e)
            {
                try { if (Clipboard.ContainsText()) { _avant.Text = Clipboard.GetText(); _fichierSource = null; } }
                catch { }
            };
            var nettoyer = Bouton("Nettoyer", "nettoyer", ref x, 116, true);
            nettoyer.Click += delegate(object s, EventArgs e) { Nettoyer(); };
            var apercu = Bouton("Ouvrir l'aperçu", "apercu", ref x, 146, false);
            apercu.Click += delegate(object s, EventArgs e) { OuvrirApercu(); };
            var copier = Bouton("Copier", "copier", ref x, 96, false);
            copier.Click += delegate(object s, EventArgs e)
            {
                try { if (_resultat != null && _resultat.Length > 0) Clipboard.SetText(_resultat); }
                catch { }
            };
            var enregistrer = Bouton("Enregistrer sous…", "enregistrer", ref x, 156, false);
            enregistrer.Click += delegate(object s, EventArgs e) { Enregistrer(); };
            var regles = Bouton("Règles…", "regles", ref x, 100, false);
            regles.Click += delegate(object s, EventArgs e) { OuvrirRegles(); };

            _lAvant = new Label();
            _lAvant.Text = IntituleParDefaut;
            _lAvant.ForeColor = Theme.TexteDoux;
            _lAvant.AutoEllipsis = true;
            _lAvant.SetBounds(20, 114, 940, 18);
            var lAvant = _lAvant;

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

            _rapport = new RoundedList();
            _rapport.Font = new Font("Segoe UI", 9f);

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

        // Un bouton de la barre, posé à x ; x avance de sa largeur + 8.
        private RoundedButton Bouton(string texte, string icone, ref int x, int largeur, bool primaire)
        {
            var b = new RoundedButton();
            b.Text = texte;
            b.Icone = icone;
            b.SetBounds(x, 72, largeur, 32);
            Theme.StyleButton(b, primaire);
            Controls.Add(b);
            x += largeur + 8;
            return b;
        }

        private RoundedField ZoneTexte()
        {
            var t = new RoundedField();
            t.Multiline = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.Font = new Font("Segoe UI", 10f);
            t.TextBox.AcceptsReturn = true;
            t.TextBox.MaxLength = 0;   // sans ça, WinForms tronque à 32 767 caractères
            return t;
        }

        // Mise en page manuelle : le texte pleine largeur (l'aperçu vit dans
        // le navigateur), stats + rapport en bas.
        private void Disposer()
        {
            var hautZones = 136;
            _lAvant.Width = ClientSize.Width - 40;
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
            Theme.Sombre(this);
        }

        private const string IntituleParDefaut =
            "Votre texte (collez, déposez ou ouvrez) — le résultat se relit via " +
            "« Ouvrir l'aperçu », mis en page dans le navigateur :";
        private const string IntituleImport =
            "Manuscrit importé — les antislashs (\\#, 1\\., \\*) sont des marques Markdown " +
            "de transport, pas un bug : absents de l'aperçu et du fichier exporté (vérifiez-le).";

        // ------------------------------------------------- mises à jour

        // Au lancement : vérification silencieuse en arrière-plan ; s'il y
        // a plus récent, le menu Aide porte un point et le statut le dit.
        private void VerifierMisesAJourEnFond()
        {
            var t = new System.Threading.Thread(delegate()
            {
                Updater.Info info;
                try { info = Updater.Verifier(); }
                catch { return; }
                if (!Updater.PlusRecente(info.Version, Updater.VersionLocale(_appDir))) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed) return;
                        _maj = info;
                        _aide.Text = "Aide  ●";
                        _aide.ForeColor = Theme.OrClair;
                        _verifierMaj.Text = "Installer la version " + info.Version + "…";
                        if (_status.Text.Length == 0)
                        {
                            _status.Text = "Typonanny " + info.Version + " est disponible — menu Aide.";
                            _status.ForeColor = Theme.OrClair;
                        }
                    });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        // Menu Aide > Vérifier les mises à jour.
        private void VerifierMisesAJour()
        {
            var info = _maj;
            if (info == null)
            {
                Cursor = Cursors.WaitCursor;
                try { info = Updater.Verifier(); }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    MessageDialog.Show(this, "Impossible de vérifier les mises à jour : " + ex.Message,
                        "Mises à jour", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Cursor = Cursors.Default;
                if (!Updater.PlusRecente(info.Version, Updater.VersionLocale(_appDir)))
                {
                    MessageDialog.Show(this, "Vous avez la dernière version de Typonanny (" +
                        Updater.VersionLocale(_appDir) + ").", "Mises à jour",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            using (var d = new MiseAJourDialog(info, _appDir))
                d.ShowDialog(this);
        }

        private void APropos()
        {
            MessageDialog.Show(this, "Typonanny " + Updater.VersionLocale(_appDir) +
                " — la nounou de votre typographie française.\n\n" +
                "Une application de la famille Stargazer, par Rémi Escamilla.\n" +
                "github.com/" + Updater.Depot, "À propos", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private bool _installationFaite;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_installationFaite) return;
            _installationFaite = true;
            VerifierMisesAJourEnFond();
            // Pandoc absent ? Il se télécharge tout seul (aller-retour .docx
            // complet et aperçu riche). L'app marche aussi sans lui.
            if (PontDocuments.TrouverPandoc(_appDir) == null)
            {
                var deps = new List<string[]>();
                deps.Add(new string[] { "Pandoc (documents .docx/.odt, aperçu riche)",
                    Path.Combine(Path.Combine(_appDir, "scripts"), "install-pandoc.ps1") });
                using (var dlg = new FenetreInstallation(_appDir, deps))
                    dlg.ShowDialog(this);
            }
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
                        _avant.Text = PontDocuments.ImporterEnMarkdown(pandoc, chemin,
                            _options.Separateurs());
                        _lAvant.Text = IntituleImport;
                        _lAvant.ForeColor = Theme.OrClair;
                        _formatSource = ext.TrimStart('.');
                        _status.Text = chemin + " — importé via Pandoc : " +
                            "l'enregistrement redonnera un ." + _formatSource +
                            " avec ses styles d'origine.";
                        _status.ForeColor = Theme.Ok;
                    }
                    else
                    {
                        _avant.Text = PontDocuments.ExtraireTexteBrut(chemin);
                        _lAvant.Text = IntituleParDefaut;
                        _lAvant.ForeColor = Theme.TexteDoux;
                        _formatSource = ext == ".docx" ? "docx" : null;
                        _status.Text = chemin + " — texte extrait sans mise en " +
                            "forme (Pandoc manque : il se télécharge au prochain " +
                            "lancement, connexion requise)." +
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
                    _lAvant.Text = IntituleParDefaut;
                    _lAvant.ForeColor = Theme.TexteDoux;
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
                MessageDialog.Erreur(this, "Impossible de lire ce fichier.\n\n" + chemin +
                    "\n\n" + ex.Message, ex);
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
            _avantNettoye = _avant.Text;
            _corrige = r.Texte;
            _etapes = r.Etapes;
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
                    Apercu.Construire(nomDoc, _avantNettoye, _etapes, _resultat, pandoc,
                        _options.Separateurs()),
                    new UTF8Encoding(true));
                System.Diagnostics.Process.Start(chemin);
                _status.Text = "Aperçu ouvert dans le navigateur — " + chemin;
                _status.ForeColor = Theme.Ok;
            }
            catch (Exception ex)
            {
                _status.Text = "Impossible d'ouvrir l'aperçu : " + ex.Message;
                _status.ForeColor = Theme.Erreur;
                MessageDialog.Erreur(this, "Impossible d'ouvrir l'aperçu.\n\n" + ex.Message, ex);
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
                                "en .txt/.md ; Pandoc se télécharge au prochain " +
                                "lancement (connexion requise).");
                        // le document d'origine sert de gabarit de styles
                        PontDocuments.ExporterDepuisMarkdown(pandoc, _resultat,
                            dlg.FileName, memeFormat ? _fichierSource : null,
                            _options.Separateurs());
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
                    MessageDialog.Erreur(this, "Impossible d'enregistrer.\n\n" + dlg.FileName +
                        "\n\n" + ex.Message, ex);
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

}
