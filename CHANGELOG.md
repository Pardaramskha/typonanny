# Changelog — Typonanny

## 1.6.0 — 2026-09-09

- **Typonanny prend son indépendance.** Elle quitte le dépôt du hub
  Stargazer pour vivre dans le sien (github.com/Pardaramskha/typonanny),
  d'où le hub la propose sous « Applications disponibles ». Le dépôt se
  range : `src/` (un fichier par classe), `tools/` (build.bat, release.ps1,
  setup-stub.cs, make-icon.ps1), `assets/`, `scripts/`, et
  `typonanny.stargazer.json` remplace `manifest.json`. L'édition macOS vit
  sous `mac/` avec son propre hôte (`mac/lib/host.js`) et son `deps.sh` —
  elle ne dépend plus du dossier `mac/` du hub.
- **Release installable.** `tools/release.ps1` fabrique l'archive portable
  Windows (celle que le hub télécharge), l'installeur autonome
  `Typonanny-Setup` (%LOCALAPPDATA%\Programs, raccourcis, désinstallation
  depuis Paramètres, aucun droit admin), l'archive des sources mac et
  l'auto-installeur `.command` (déballe dans ~/Applications et compile
  l'applet avec l'osacompile du Mac).
- **Pandoc dans un dossier partagé.** Hors du hub, il va dans
  `%LOCALAPPDATA%\Stargazer\dependencies` (mac : `~/Library/Application
  Support/Stargazer/dependencies`), commun à toutes les apps de la famille
  — jamais téléchargé deux fois. Dans le hub, toujours `<hub>\dependencies` ;
  `STARGAZER_DEPS` prime sur les deux. Plus de `bin\` local.
- **Nouvelle icône** (le visuel de Rémi) et bannière du README.
- **L'habillage rejoint la norme de la famille** (Marabook, Markdown we
  go, My Somehow Legal Downloader) : boutons arrondis à icônes Phosphor,
  zone de texte et rapport dans des cadres arrondis (bordure d'or au
  focus), barre de titre sombre sur toutes les fenêtres, bouton principal
  à droite. Les erreurs de lecture, d'enregistrement et d'aperçu s'affichent
  dans une boîte de dialogue maison (pastille de sens, « Détails
  techniques » repliés) en plus de la ligne de statut. Les messages ne
  renvoient plus vers Skadoosh : Pandoc se télécharge tout seul.


## 1.5.0 — 2026-07-19

- **Pandoc s'installe tout seul.** S'il manque à l'ouverture, la fenêtre
  « Premiers préparatifs » le télécharge directement (dossier de
  dépendances partagé, `scripts/install-pandoc.ps1`) — aller-retour
  `.docx`/`.odt` et aperçu riche garantis dès le premier lancement.
  « Continuer en arrière-plan » disponible ; sans lui, l'app garde ses
  modes simplifiés.

## 1.4.0 — 2026-07-19

- **L'aperçu montre où la nounou est passée.** Nouvelle vue
  « Corrections » (par défaut) dans l'aperçu navigateur, à la manière de
  One di-version : chaque retouche est surlignée au caractère près —
  supprimé barré sur fond rouge, inséré sur fond vert (les fines
  insécables deviennent enfin visibles !), lignes intactes en doux. Un
  bouton bascule vers la vue « Mise en page » (le rendu riche
  d'avant). En mode « Signaler seulement », la vue Corrections montre ce
  qui *serait* corrigé.

## 1.3.0 — 2026-07-19

- **L'aperçu déménage dans le navigateur.** Nouveau bouton « 👁 Ouvrir
  l'aperçu » : le résultat nettoyé est écrit dans `exports\apercu.html`
  (un seul fichier, écrasé à chaque fois) puis ouvert dans le navigateur
  par défaut — gras, italiques, exposants, titres et listes rendus
  proprement (via Pandoc, avec un mini-rendu Markdown maison en repli),
  cadratins et insécables affichés tels quels. En-tête « Stargazer —
  Typonanny » avec le nom du document travaillé en sous-titre, mise en
  page lecture (sérif, colonne confortable) aux couleurs de la famille.
- **La zone « Après » disparaît** : votre texte prend toute la largeur,
  la relecture se fait dans l'aperçu. « Copier » et « Enregistrer
  sous… » utilisent toujours le dernier résultat nettoyé (modifier le
  texte l'invalide, re-nettoyer le régénère — l'aperçu le fait tout
  seul si besoin).
- Les balises HTML égarées dans un manuscrit s'affichent au lieu de
  s'exécuter dans l'aperçu. Le dossier `exports\` est exclu de la
  valise et du dépôt.

## 1.2.0 — 2026-07-19

- **Les styles du .docx sont conservés à l'identique.** Enregistrer un
  `.docx` corrigé ne repasse plus par Markdown : la typographie est
  corrigée **directement dans le document** (chirurgie de
  `word/document.xml`) — chaque paragraphe est nettoyé d'un bloc, puis
  le texte corrigé est redistribué sur les runs d'origine par un diff
  caractère par caractère. Gras, italiques, couleurs, polices, styles de
  paragraphe : rien ne bouge, seuls les caractères fautifs changent.
  Marche même sans Pandoc.
- Si le texte importé a été retouché à la main, l'enregistrement repasse
  par Pandoc avec le document d'origine en **gabarit de styles**
  (`--reference-doc`) et le statut l'explique. Idem pour les `.odt`.

## 1.1.0 — 2026-07-19

- **Manuscrits mis en forme : .docx et .odt acceptés.** Avec Pandoc
  (installé d'un clic via Skadoosh, dossier de dépendances partagé du
  hub), le document entre en Markdown éditable — sans coupures de ligne
  artificielles — et « Enregistrer sous » redonne un `.docx`/`.odt`
  `-typo` avec gras, titres et listes conservés ; les insécables et
  ligatures survivent à l'aller-retour. Sans Pandoc, le texte brut est
  extrait nativement du XML (lecture seule, sortie `.txt`/`.md`) et le
  statut explique comment obtenir l'aller-retour complet.
- **Plus de plafond à 32 767 caractères** : les zones de texte acceptent
  désormais les vrais gros manuscrits (limite WinForms par défaut levée).

## 1.0.0 — 2026-07-19

Première version de la nounou typographique — fusion des codenames
« Espace vital » (spec complète de l'EDITION-ROADMAP) et « Signe des
temps ». 100 % natif.

- **Nettoyage typographique français** avec aperçu avant/après et rapport
  détaillé par catégorie : apostrophes courbes, `...` → `…` (et
  `etc...` → `etc.`), guillemets `"…"` → `« … »` avec insécables
  intérieures, tirets de dialogue `—`, intervalles `1914–1918`,
  insécables de ponctuation (fine avant `; ! ?`, pleine avant `:` en IN
  strict), unités `10 %` / `10 €` / `12 kg`, milliers en fine `10 000`,
  ligatures `œ` par liste blanche, dimensions `10 × 15`, ordinaux
  `2ème → 2e`, doubles espaces et fins de ligne.
- **Trois préréglages** : Imprimerie nationale (strict), Souple (fine
  partout), Minimal (évidences seules) — plus une case par règle.
- **Règles éditables** : la liste des ligatures est un simple
  `config\ligatures.txt` (+ `ligatures-ae.txt`) relu à chaque lancement ;
  les réglages vivent dans `config\typo.conf` en clé=valeur.
- **Zones protégées** : URLs, e-mails, chemins, code Markdown, heures et
  ratios ne sont jamais modifiés.
- **Signalements sans correction** : plus de trois points, guillemets
  déséquilibrés, majuscules à accentuer (`Etat` → `État`) — signalés,
  jamais imposés. Mode « Signaler seulement » disponible.
- **Comptage du métier** (Signe des temps) : SEC, signes sans espaces,
  mots, **feuillets de 1 500 signes**, temps de lecture — en direct sous
  le texte, avec ventilation par chapitre si le texte a des titres `#`.
- **Idempotence garantie** : repasser un texte déjà propre ne change
  rien (testé sur le jeu de la spec).
- L'original n'est jamais écrasé : sortie en fichier `-typo` ou
  presse-papier. Échap ferme, Ctrl+Entrée nettoie.
