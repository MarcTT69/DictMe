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

Écrit en C# avec **.NET MAUI** (.NET 10).

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

## iPhone

Le projet cible aussi iOS (`net10.0-ios`), mais Apple impose de compiler sur un Mac avec Xcode et de signer avec un
compte développeur Apple : `dotnet build src/DictMe.App -f net10.0-ios` sur macOS.

## Développer

Prérequis : SDK .NET 10 et le workload MAUI (`dotnet workload install maui-android`), ou Visual Studio 2022/2026
avec la charge de travail « Développement d'applications multiplateformes .NET ».

```bash
dotnet test tests/DictMe.Core.Tests                         # tests unitaires (aucun SDK mobile requis)
dotnet build src/DictMe.App -f net10.0-android -t:Run       # lancer sur un téléphone/émulateur Android branché
```

### Structure

```
src/DictMe.Core/          Logique indépendante de l'interface (testée)
  Audio/                  Lecture WAV, conversion en mono 16 kHz pour Whisper
  Text/                   Nettoyage de la sortie Whisper, commandes vocales
  Models/                 Catalogue et téléchargement des modèles Whisper
  History/                Historique des dictées (JSON local)
  Transcription/          WhisperTranscriber + DictationPipeline (WAV → texte)
src/DictMe.App/           Application .NET MAUI (MVVM, CommunityToolkit.Mvvm)
  Services/               Enregistrement micro (Plugin.Maui.Audio), réglages
  ViewModels/  Views/     Écrans Dicter, Historique, Réglages
tests/DictMe.Core.Tests/  Tests xUnit
```

### Fonctionnement

1. `RecordingService` enregistre le micro en WAV 44,1 kHz mono (fréquence garantie sur tous les Android).
2. `WavReader` + `AudioConverter` convertissent en float mono 16 kHz.
3. `WhisperTranscriber` exécute whisper.cpp localement, avec un « prompt » qui l'incite à ponctuer et contient votre vocabulaire.
4. `DictationFormatter` supprime les annotations (`[BLANK_AUDIO]`, `(Musique)`, hallucinations type « Sous-titres… Amara.org »),
   applique les commandes vocales et corrige espaces, ponctuation et majuscules.

## Pistes pour la suite

- Clavier Android personnalisé (IME) avec un bouton micro, pour dicter dans n'importe quelle application sans copier-coller.
- Tuile de réglages rapides / widget pour lancer une dictée en un geste.
- Reformulation optionnelle du texte (ton, résumé) par un LLM.
