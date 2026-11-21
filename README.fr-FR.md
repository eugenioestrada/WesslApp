# Wessl

[English](README.md) · [Español](README.es-ES.md) · [简体中文](README.zh-CN.md) · [Français](README.fr-FR.md) · [Italiano](README.it-IT.md) · [Deutsch](README.de-DE.md)

Wessl est une application native légère de la zone de notification Windows pour WSL Containers (WSLc), développée avec Microsoft.UI.Reactor et WinUI 3. Elle démarre dans la zone de notification : cliquez avec le bouton gauche sur l’icône pour ouvrir la fenêtre des conteneurs à côté de la zone, ou avec le bouton droit pour afficher le menu WinUI natif Ouvrir et Quitter.

L’ouverture depuis la zone de notification affiche le panneau des conteneurs et lui donne le focus avec un bref fondu d’entrée. Lorsque le focus passe à une autre application ou fenêtre d’outil, le panneau disparaît avec un fondu tandis que l’application reste dans la zone de notification. Une nouvelle ouverture annule tout fondu de sortie en attente. Le panneau ne reste pas toujours au premier plan et les transitions respectent le paramètre d’animation de Windows.

Consultez la [feuille de route](ROADMAP.md) pour connaître les étapes prévues et les priorités fonctionnelles.

## Pourquoi « Wessl » ?

**Wessl** est un mot-valise formé de **WSL** et de **Vessel**, un terme qui désigne à la fois un navire transportant des conteneurs et un conteneur lui-même. Il conserve la référence maritime de l’écosystème des conteneurs sans recourir aux clichés habituels. Il se prononce comme *vessel* (avec un *W*), en accord avec la philosophie de conception courte, claire et légère de l’application.

L’application utilise le véritable client WSLc par défaut. L’exécutable `wslc` doit se trouver dans `PATH`, et sa commande de liste doit prendre en charge `wslc container list --all --format json`, avec un objet JSON par ligne. Si WSLc ne peut pas démarrer, l’application affiche des instructions pour mettre WSL à jour ainsi qu’un bouton permettant de copier `wsl --update` ; elle ne se rabat pas sur des données d’exemple. Pour utiliser des exemples de conteneurs et d’images lors d’une démonstration, définissez `WESSL_USE_SAMPLE_WSLC=1` avant de lancer l’application. Les actions Démarrer, Arrêter et Redémarrer sont disponibles sur chaque ligne ; la suppression demande une confirmation et peut arrêter un conteneur en cours d’exécution.

Les mappages TCP publiés dans le champ `Ports` d’un conteneur fournissent également des URL localhost à ouvrir ou à copier.

Ouvrez **Images** pour charger explicitement la liste des images WSLc et les tailles indiquées. Actualiser recharge la liste. **Nettoyer les images inutilisées** demande confirmation et supprime toutes les images référencées par aucun conteneur, y compris les images étiquetées ; le résultat de WSLc et les erreurs éventuelles sont affichés. Les opérations sur les images peuvent démarrer WSL et ne sont lancées qu’à la suite d’une action de l’utilisateur.

Pour un conteneur en cours d’exécution, **Ouvrir le terminal** lance Windows Terminal avec une session interactive `wslc exec -it <container-id> /bin/sh`. Cette action nécessite une véritable intégration WSLc ; Windows Terminal et une version de WSLc prenant en charge `exec -it` doivent être disponibles.

**Afficher les journaux** ouvre une fenêtre séparée qui suit `wslc container logs -f <container-id>`. Fermer cette fenêtre arrête le processus de suivi. L’affichage conserve les 128 Ki les plus récents.

L’état des conteneurs est actualisé à l’ouverture de la fenêtre, puis toutes les 10 secondes tant qu’elle reste visible. Le bouton Actualiser permet également de demander une mise à jour immédiate. La vérification périodique s’arrête lorsque la fenêtre est masquée, réduite ou fermée ; le simple démarrage de l’application dans la zone de notification n’invoque pas WSLc. L’indicateur de la zone affiche un état inconnu jusqu’à la première actualisation réussie, puis reflète le dernier état connu lorsque la fenêtre est fermée.

## Prérequis

- Windows 10 version 2004 (build 19041) ou ultérieure, x64 ou ARM64.
- .NET 11 SDK et le Windows SDK / les outils de compilation WinUI (par exemple Visual Studio avec les charges de travail Développement .NET Desktop et Développement d’applications Windows).
- Pour l’intégration réelle : WSL avec WSL Containers et `wslc` dans `PATH`. Consultez le [guide WSL Containers](https://learn.microsoft.com/en-us/windows/wsl/tutorials/wsl-containers).

## Compiler et exécuter

Depuis un terminal de développement Windows, à la racine du dépôt :

```powershell
dotnet restore Wessl.slnx
dotnet build Wessl.slnx
dotnet run --project src/Wessl.App/Wessl.App.csproj
dotnet test tests/Wessl.Tests/Wessl.Tests.csproj
```

Le fichier `NuGet.Config` du dépôt restaure les packages depuis nuget.org, indépendamment des autres sources de packages configurées sur votre machine.

Pour ARM64, transmettez `-p:RuntimeIdentifier=win-arm64` aux commandes de restauration, de compilation et d’exécution à la place de `win-x64`, la valeur par défaut. L’application n’est pas empaquetée et inclut le runtime Windows App SDK. La compilation de l’application WinUI nécessite Windows ; les tests Domain et Infrastructure peuvent s’exécuter sur toute plateforme prise en charge par .NET 11.

## Localisation

L’application WinUI utilise les ressources `.resw` MRT Core du Windows App SDK dans `src/Wessl.App/Strings/<BCP-47>/Resources.resw`. L’anglais (`en-US`), l’espagnol (`es-ES`), le chinois simplifié (`zh-CN`), le français (`fr-FR`), l’italien (`it-IT`) et l’allemand (`de-DE`) sont pris en charge et sélectionnables dans les paramètres. Pour ajouter une traduction, créez un dossier de langue contenant les mêmes clés de ressource ; la compilation Windows App SDK indexe ces fichiers dans les ressources PRI de l’application.

## Publication NativeAOT

Des publications NativeAOT non empaquetées sont disponibles pour Windows x64 et ARM64. Exécutez les commandes suivantes sous Windows, avec .NET 11 SDK et Visual C++ Build Tools (charge de travail Développement Desktop en C++) installés :

```powershell
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-x64-nativeaot
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-arm64-nativeaot
```

Les artefacts sont générés dans :

- `src/Wessl.App/bin/publish/win-x64-nativeaot/`
- `src/Wessl.App/bin/publish/win-arm64-nativeaot/`

Chaque artefact contient `wessl.exe`. Les compilations NativeAOT désactivent les données de globalisation avec `InvariantGlobalization=true` et ne prennent pas en charge la réflexion ni le code dynamique en dehors de la surface du framework intégrée à la compilation AOT. L’application évite actuellement le code fortement dépendant de la réflexion et `Reactor.DevtoolsSupport` n’est pas activé dans les compilations Release. La publication ne modifie pas le processus normal de compilation ou d’exécution.

La publication NativeAOT nécessite de cibler l’architecture Windows correspondante. La publication croisée ARM64 depuis Windows x64 requiert les outils de compilation croisée Visual C++ appropriés et le résultat n’est pas censé s’exécuter sur la machine de publication.

## Distribution des versions

Les workflows GitHub Actions compilent et testent les demandes de tirage et les poussées vers `main`. La création d’une étiquette de version telle que `v1.2.3` déclenche le workflow de publication, qui génère des archives NativeAOT pour `win-x64` et `win-arm64` et crée une GitHub Release avec des notes générées. Le workflow rejette les étiquettes qui ne respectent pas le format `vMAJOR.MINOR.PATCH` (un suffixe de préversion facultatif est accepté), refuse de remplacer une version existante et utilise le `GITHUB_TOKEN` du dépôt avec des droits d’écriture uniquement dans le travail de publication.

La version inclut également un ensemble généré de manifestes winget avec les valeurs SHA-256 exactes des archives. Le workflow ne soumet ni packages ni manifestes au dépôt communautaire winget ; cette soumission est une action distincte de la personne responsable de la maintenance, après examen des artefacts. Une fois le package accepté dans winget, vous pouvez l’installer avec :

```powershell
winget install --id eugenioestrada.Wessl --exact
```

Chaque archive contient l’intégralité de la sortie de publication non empaquetée. Le manifeste winget enregistre `wessl.exe` comme alias de commande portable ; Wessl peut également être lancé directement depuis le dossier extrait.

## Arborescence

- `src/Wessl.Domain` : types de conteneurs sans dépendances et interface client.
- `src/Wessl.Infrastructure` : client d’exemple et processus WSLc asynchrone / analyseur JSON.
- `src/Wessl.App` : composants Reactor, cycle de vie de la zone de notification et injection de dépendances.
- `tests/Wessl.Tests` : tests du domaine et de l’analyseur.
