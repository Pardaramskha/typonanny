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

// ReglesDialog.cs — la fenêtre des règles : trois préréglages, une case
// par règle, et le mode « Signaler seulement ».

namespace Typonanny
{
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
            Theme.Dialogue(this, "Règles typographiques — Typonanny");
            Font = new Font("Segoe UI", 9f);   // deux colonnes de cases : serré
            ClientSize = new Size(500, 520);

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
            ok.SetBounds(384, 472, 100, 32);
            Theme.StyleButton(ok, true);
            ok.Click += delegate(object s, EventArgs e) { Valider(); };

            var cancel = new RoundedButton();
            cancel.Text = "Annuler";
            cancel.SetBounds(280, 472, 96, 32);
            Theme.StyleButton(cancel, false);
            cancel.Click += delegate(object s, EventArgs e) { DialogResult = DialogResult.Cancel; };

            Controls.Add(titrePre); Controls.Add(_in); Controls.Add(_souple);
            Controls.Add(_minimal); Controls.Add(titreRegles);
            Controls.Add(_signaler); Controls.Add(note);
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Sombre(this);
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

}
