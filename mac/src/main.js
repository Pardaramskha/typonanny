// main.js — Typonanny pour macOS (concaténé après host.js).
// La nounou typographique vit dans ui/index.html : le moteur (règles,
// comptage, diff de Myers, aperçu) est du JavaScript côté page. Ici, le
// strict nécessaire natif : presse-papier, lecture de fichiers avec
// détection de BOM/encodage, dossier temporaire, fichier ouvert via
// l'icône (équivalent de l'argument CLI Windows).

Stargazer.logName = 'typonanny';

// ------------------------------------------------------- presse-papier
// Clipboard.GetText/SetText → NSPasteboard (pbpaste/pbcopy maltraitent
// les fins de ligne via doShellScript, alors on parle direct à Cocoa).

Stargazer.commands['typo.coller'] = function () {
  var pb = $.NSPasteboard.generalPasteboard;
  var s = pb.stringForType($.NSPasteboardTypeString);
  return { text: s.isNil() ? null : s.js };
};

Stargazer.commands['typo.copier'] = function (args) {
  var pb = $.NSPasteboard.generalPasteboard;
  pb.clearContents;
  pb.setStringForType($(String(args.text)), $.NSPasteboardTypeString);
  return { ok: true };
};

// ------------------------------------------------- lecture de fichiers
// File.ReadAllText : UTF-8 d'abord (BOM détecté et retiré, mémorisé pour
// réécrire pareil), repli Windows-1252 pour les vieux .txt.

Stargazer.commands['typo.lireTexte'] = function (args) {
  var data = $.NSData.dataWithContentsOfFile($(args.path));
  if (data.isNil()) return { err: 'fichier introuvable ou illisible' };
  var s = $.NSString.alloc.initWithDataEncoding(data, $.NSUTF8StringEncoding);
  if (s.isNil())
    s = $.NSString.alloc.initWithDataEncoding(data, $.NSWindowsCP1252StringEncoding);
  if (s.isNil()) return { err: 'encodage du fichier non reconnu' };
  var texte = s.js;
  var bom = texte.length > 0 && texte.charCodeAt(0) === 0xFEFF;
  if (bom) texte = texte.slice(1);
  return { text: texte, bom: bom };
};

Stargazer.commands['typo.tempDir'] = function () {
  return { dir: $.NSTemporaryDirectory().js };
};

// ------------------------------------------- fichier ouvert avec l'app
// L'équivalent mac de l'argument CLI : un fichier déposé sur l'icône (ou
// « Ouvrir avec ») arrive par openDocuments. Si la page n'est pas encore
// prête, elle le récupère au démarrage via typo.fichierInitial.

var fichierInitial = null;

Stargazer.commands['typo.fichierInitial'] = function () {
  var p = fichierInitial;
  fichierInitial = null;
  return { path: p };
};

function openDocuments(docs) {
  try {
    if (!docs || !docs.length) return;
    var p = String(docs[0]);
    var mw = Stargazer.mainWindow();
    if (mw)
      Stargazer.pushToPage(mw.webview, "__push.bind(null,'ouvrirFichier')",
        { path: p });
    else fichierInitial = p;
  } catch (e) { Stargazer.journal('openDocuments : ' + e); }
}

// ------------------------------------------------------------- fenêtre

Stargazer.createWindow({
  title: 'Typonanny', page: 'index.html',
  width: 980, height: 640,
  minWidth: 900, minHeight: 520
});
