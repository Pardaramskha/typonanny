# Changelog — Typonanny

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
