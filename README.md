# DictMe 🎙

Dictée vocale **gratuite** pour téléphone, dans l'esprit de Wispr Flow ou Dicto : vous parlez, DictMe transcrit,
le texte est copié dans le presse-papiers, prêt à coller dans n'importe quelle application.

- **100 % gratuit, sans compte, sans abonnement** : la transcription utilise [Whisper](https://github.com/openai/whisper)
  (via [whisper.cpp](https://github.com/ggerganov/whisper.cpp) / [Whisper.net](https://github.com/sandrohanea/whisper.net))
  directement sur le téléphone.
- **Hors ligne et privé** : après le téléchargement du modèle (une seule fois), plus besoin d'internet. Votre voix ne quitte jamais l'appareil.
- **Ponctuation automatique** et **commandes vocales** : « à la ligne », « nouveau paragraphe », « virgule »,
  « point d'interrogation », « point d'exclamation », « point-virgule », « points de suspension », « point final ».
- **Vocabulaire personnalisé** (noms propres, jargon) pour aider Whisper à bien les écrire.
- **Historique** local des dictées : copier, partager, supprimer. Le texte est modifiable avant copie.
- Français, anglais, espagnol, allemand, italien, portugais ou détection automatique.

Écrit en C# (.NET 10) : une **web app installable** (Blazor WebAssembly) pour iPhone, et une app **.NET MAUI** pour Android.

## Installer sur iPhone

DictMe pour iPhone est une web app (PWA) : pas d'App Store, pas de Mac, pas de compte développeur.

1. Ouvrez **https://marctt69.github.io/DictMe/** dans **Safari**.
2. Touchez **Partager** ⬆️ puis **Sur l'écran d'accueil**. DictMe s'ouvre ensuite comme une app, en plein écran.
3. Onglet **Réglages** : téléchargez le modèle **Base** (~80 Mo, une seule fois). Autorisez le micro quand Safari le demande.
4. Onglet **Dicter** : touchez le micro, parlez, touchez à nouveau. Le texte est copié : collez-le où vous voulez.

La transcription tourne dans Safari avec [transformers.js](https://github.com/huggingface/transformers.js)
(Whisper au format ONNX, en WebAssembly). Le moteur et le modèle sont mis en cache : après la première
utilisation, DictMe fonctionne hors ligne.

> **Mise en ligne** : la web app est publiée sur GitHub Pages par le workflow `web.yml` à chaque push sur `main`.
> Il faut l'activer une fois : **Settings → Pages → Source : GitHub Actions**. GitHub Pages n'est gratuit que
> pour les dépôts **publics** (un dépôt privé demande un abonnement GitHub Pro).

## Installer sur un téléphone Android

1. Ouvrez l'onglet **Actions** du dépôt sur GitHub, choisissez le dernier build réussi et téléchargez l'artefact
   **DictMe-android** (un zip contenant `DictMe.apk`). Pour une version stable, poussez un tag `v1.0` :
   l'APK est alors attaché à une **Release**, téléchargeable directement depuis le téléphone.
2. Sur le téléphone, ouvrez `DictMe.apk` et autorisez l'installation d'applications de cette source.
3. Au premier lancement : onglet **Réglages** → téléchargez un modèle (**Base**, ~60 Mo, recommandé pour commencer ;
   **Small**, ~190 Mo, plus précis en français sur un téléphone récent).
4. Onglet **Dicter** : touchez le micro, parlez, touchez à nouveau. Le texte est copié automatiquement.

> Sans clé de signature configurée, chaque build est signé avec une clé de debug différente : pour installer une
> nouvelle version, désinstallez d'abord l'ancienne. Pour l'éviter, ajoutez dans les secrets du dépôt
> `ANDROID_KEYSTORE_BASE64` (votre keystore encodé en base64), `ANDROID_KEYSTORE_PASSWORD` et `ANDROID_KEY_ALIAS`.

## App iOS native

Le projet MAUI cible aussi iOS (`net10.0-ios`), mais Apple impose de compiler sur un Mac avec Xcode et de signer
avec un compte développeur Apple : `dotnet build src/DictMe.App -f net10.0-ios` sur macOS. La web app ci-dessus
évite ces contraintes.

## Développer

Prérequis : SDK .NET 10 et le workload MAUI (`dotnet workload install maui-android`), ou Visual Studio 2022/2026
avec la charge de travail « Développement d'applications multiplateformes .NET ».

```bash
dotnet test tests/DictMe.Core.Tests                         # tests unitaires (aucun SDK mobile requis)
dotnet run --project src/DictMe.Web                         # web app sur http://localhost:5000
dotnet build src/DictMe.App -f net10.0-android -t:Run       # lancer sur un téléphone/émulateur Android branché
```

### Structure

```
src/DictMe.Core/          Logique indépendante de l'interface (testée)
  Audio/                  Lecture WAV, conversion en mono 16 kHz pour Whisper
  Text/                   Nettoyage de la sortie Whisper, commandes vocales
  Models/                 Catalogue et téléchargement des modèles Whisper
  History/                Historique des dictées (JSON local)
  Transcription/          DictationPipeline (WAV → texte), interface ITranscriber
src/DictMe.Web/           Web app Blazor WebAssembly (iPhone, PWA hors ligne)
  Services/               État de l'app, pont JavaScript, catalogue des modèles ONNX
  Components/             Onglets Dicter, Historique, Réglages
  wwwroot/js/             Micro et audio (dictme.js), Whisper dans un Web Worker (whisper-worker.js)
src/DictMe.App/           Application .NET MAUI pour Android (MVVM, CommunityToolkit.Mvvm)
  Services/               Micro (Plugin.Maui.Audio), Whisper.net (whisper.cpp natif), réglages
  ViewModels/  Views/     Écrans Dicter, Historique, Réglages
tests/DictMe.Core.Tests/  Tests xUnit
```

### Fonctionnement (Android)

1. `RecordingService` enregistre le micro en WAV 44,1 kHz mono (fréquence garantie sur tous les Android).
2. `WavReader` + `AudioConverter` convertissent en float mono 16 kHz.
3. `WhisperTranscriber` exécute whisper.cpp localement, avec un « prompt » qui l'incite à ponctuer et contient votre vocabulaire.
4. `DictationFormatter` supprime les annotations (`[BLANK_AUDIO]`, `(Musique)`, hallucinations type « Sous-titres… Amara.org »),
   applique les commandes vocales et corrige espaces, ponctuation et majuscules.

Sur iPhone, `dictme.js` enregistre avec `MediaRecorder`, décode et rééchantillonne en 16 kHz avec la Web Audio API,
puis `whisper-worker.js` transcrit ; le même `DictationFormatter` (C#) met en forme le texte.

## Pistes pour la suite

- Raccourci iOS (app Raccourcis) qui ouvre DictMe et colle le texte.
- Accélération WebGPU dans Safari quand elle sera fiable sur iPhone.
- Clavier Android personnalisé (IME) avec un bouton micro, pour dicter dans n'importe quelle application sans copier-coller.
- Tuile de réglages rapides / widget pour lancer une dictée en un geste.
- Reformulation optionnelle du texte (ton, résumé) par un LLM.
