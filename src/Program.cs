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
