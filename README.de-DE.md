# Wessl

[English](README.md) · [Español](README.es-ES.md) · [简体中文](README.zh-CN.md) · [Français](README.fr-FR.md) · [Italiano](README.it-IT.md) · [Deutsch](README.de-DE.md)

Wessl ist eine leichte native Windows-Infobereich-App für WSL Containers (WSLc), entwickelt mit Microsoft.UI.Reactor und WinUI 3. Die App startet im Infobereich: Klicken Sie mit der linken Maustaste auf das Symbol, um das Containerfenster neben dem Infobereich zu öffnen, oder mit der rechten Maustaste, um das native WinUI-Menü mit den Einträgen Öffnen und Beenden anzuzeigen.

Beim Öffnen über den Infobereich wird das Containerfenster mit einer kurzen Einblendung sichtbar und erhält den Fokus. Wechselt der Fokus zu einer anderen Anwendung oder einem Werkzeugfenster, wird das Fenster ausgeblendet, während die App im Infobereich aktiv bleibt. Beim erneuten Öffnen wird eine eventuell noch ausstehende Ausblendung abgebrochen. Das Fenster bleibt nicht immer im Vordergrund; Übergänge beachten die Windows-Einstellung für Animationen.

Die geplanten Meilensteine und Funktionsprioritäten finden Sie in der [Roadmap](ROADMAP.md).

## Warum „Wessl“?

**Wessl** ist ein Kofferwort aus **WSL** und **Vessel**. Das Wort bezeichnet sowohl ein Schiff, das Container transportiert, als auch einen Container selbst. So bleibt die nautische Anspielung des Container-Ökosystems erhalten, ohne auf die üblichen Klischees zurückzugreifen. Es wird wie *vessel* ausgesprochen (mit einem *W*) und spiegelt die kurze, klare und leichte Designphilosophie der App wider.

Standardmäßig verwendet die App den echten WSLc-Client. Die ausführbare Datei `wslc` muss sich im `PATH` befinden, und der Listenbefehl muss `wslc container list --all --format json` unterstützen und pro Zeile ein JSON-Objekt ausgeben. Kann WSLc nicht gestartet werden, zeigt die App Anweisungen zum Aktualisieren von WSL sowie eine Schaltfläche zum Kopieren von `wsl --update` an; sie wechselt nicht auf Beispieldaten. Um für eine Demo Beispielcontainer und -images zu verwenden, setzen Sie vor dem Start der App `WESSL_USE_SAMPLE_WSLC=1`. Starten, Beenden und Neustarten sind direkt in jeder Zeile möglich. Zum Entfernen ist eine Bestätigung erforderlich; ein laufender Container kann dabei angehalten werden.

Veröffentlichte TCP-Zuordnungen im Feld `Ports` eines Containers bieten außerdem localhost-URLs zum Öffnen oder Kopieren.

Öffnen Sie **Images**, um die WSLc-Image-Liste und die gemeldeten Größen ausdrücklich zu laden. Aktualisieren lädt die Liste erneut. **Ungenutzte Images bereinigen** erfordert eine Bestätigung und entfernt alle Images, auf die kein Container verweist, einschließlich getaggter Images. Das WSLc-Ergebnis und mögliche Fehler werden angezeigt. Imagevorgänge können WSL starten und werden erst nach einer Benutzeraktion ausgeführt.

Bei einem laufenden Container öffnet **Shell öffnen** Windows Terminal mit einer interaktiven Sitzung `wslc exec -it <container-id> /bin/sh`. Dafür ist die echte WSLc-Integration erforderlich; Windows Terminal und eine WSLc-Version mit Unterstützung für `exec -it` müssen verfügbar sein.

**Protokolle anzeigen** öffnet ein separates Fenster, das `wslc container logs --timestamps -f <container-id>` verfolgt. Vorhandene und neu gestreamte Zeilen enthalten Zeitstempel. Durchsuchen Sie die geladenen Protokolle, kopieren Sie ausgewählte oder sichtbare Zeilen oder speichern Sie die verfügbaren Protokolle in einer Datei. Beim Schließen des Fensters wird die Verfolgung beendet. Die Anzeige behält die letzten 128 Ki Zeichen bei.

Laufende Container zeigen CPU, Arbeitsspeicher, Netzwerk- und Block-E/A sowie die Anzahl der PIDs mit `wslc stats --format json` an. Die Statistiken werden standardmäßig jede Sekunde aktualisiert; das Intervall ist in den Einstellungen änderbar. Bei ausgeblendetem Panel wird die Abfrage angehalten.

Der Containerstatus wird beim Öffnen des Fensters und anschließend alle 10 Sekunden aktualisiert, solange es sichtbar ist. Die Schaltfläche Aktualisieren fordert ebenfalls sofort eine Aktualisierung an. Die regelmäßige Abfrage endet, sobald das Fenster ausgeblendet, minimiert oder geschlossen wird; allein der Start der App im Infobereich ruft WSLc nicht auf. Der Infobereich-Indikator zeigt bis zur ersten erfolgreichen Aktualisierung einen unbekannten Status und danach bei geschlossenem Fenster den zuletzt bekannten Status.

## Voraussetzungen

- Windows 10, Version 2004 (Build 19041) oder höher, x64 oder ARM64.
- .NET 11 SDK und Windows SDK / WinUI-Buildtools (zum Beispiel Visual Studio mit den Workloads „.NET-Desktopentwicklung“ und „Windows-Anwendungsentwicklung“).
- Für die echte Integration: WSL mit WSL Containers und `wslc` im `PATH`. Siehe den [WSL-Containers-Leitfaden](https://learn.microsoft.com/en-us/windows/wsl/tutorials/wsl-containers).

## Erstellen und ausführen

Führen Sie die folgenden Befehle in einem Windows-Entwicklerterminal im Stammverzeichnis des Repositorys aus:

```powershell
dotnet restore Wessl.slnx
dotnet build Wessl.slnx
dotnet run --project src/Wessl.App/Wessl.App.csproj
dotnet test tests/Wessl.Tests/Wessl.Tests.csproj
```

Die `NuGet.Config` des Repositorys stellt Pakete von nuget.org wieder her, unabhängig von zusätzlichen Paketquellen auf Ihrem Computer.

Für ARM64 übergeben Sie `-p:RuntimeIdentifier=win-arm64` an die Wiederherstellungs-, Build- und Ausführungsbefehle statt des Standardwerts `win-x64`. Die App ist nicht paketiert und enthält die Windows App SDK-Laufzeit. Der WinUI-App-Build erfordert Windows; Domain- und Infrastructure-Tests können auf jeder von .NET 11 unterstützten Plattform ausgeführt werden.

## Lokalisierung

Die WinUI-App verwendet Windows App SDK MRT Core `.resw`-Ressourcen in `src/Wessl.App/Strings/<BCP-47>/Resources.resw`. Englisch (`en-US`), Spanisch (`es-ES`), vereinfachtes Chinesisch (`zh-CN`), Französisch (`fr-FR`), Italienisch (`it-IT`) und Deutsch (`de-DE`) werden unterstützt und können in den Einstellungen ausgewählt werden. Für eine neue Übersetzung erstellen Sie einen Sprachordner mit denselben Ressourcenschlüsseln; der Windows App SDK-Build indexiert diese Dateien in den PRI-Ressourcen der App.

## NativeAOT-Veröffentlichung

Nicht paketierte NativeAOT-Versionen sind für Windows x64 und ARM64 verfügbar. Führen Sie die folgenden Befehle unter Windows mit installiertem .NET 11 SDK und Visual C++ Build Tools (Workload „Desktopentwicklung mit C++“) aus:

```powershell
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-x64-nativeaot
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-arm64-nativeaot
```

Die Artefakte werden in folgenden Verzeichnissen erstellt:

- `src/Wessl.App/bin/publish/win-x64-nativeaot/`
- `src/Wessl.App/bin/publish/win-arm64-nativeaot/`

Jedes Artefakt enthält `wessl.exe`. NativeAOT-Builds deaktivieren Globalisierungsdaten mit `InvariantGlobalization=true` und unterstützen keine Reflection oder dynamischen Code außerhalb der in den AOT-Build aufgenommenen Framework-Funktionen. Die App vermeidet derzeit stark von Reflection abhängigen Code; `Reactor.DevtoolsSupport` ist in Release-Builds nicht aktiviert. Die Veröffentlichung erfordert keine Änderung am normalen Build- oder Ausführungsablauf.

Für die NativeAOT-Veröffentlichung muss die passende Windows-Architektur als Ziel angegeben werden. Für eine ARM64-Cross-Veröffentlichung von Windows x64 sind die entsprechenden Visual C++-Cross-Compiler erforderlich; die Veröffentlichung ist nicht für die Ausführung auf dem erstellenden Computer vorgesehen.

## Releaseverteilung

Die GitHub-Actions-Workflows erstellen und testen Pull Requests sowie Pushes auf `main`. Das Pushen eines Versions-Tags wie `v1.2.3` startet den Release-Workflow. Dieser veröffentlicht NativeAOT-Archive für `win-x64` und `win-arm64` und erstellt eine GitHub Release mit automatisch generierten Hinweisen. Der Workflow lehnt Tags ab, die nicht dem Format `vMAJOR.MINOR.PATCH` entsprechen (ein optionales Vorabversionssuffix ist zulässig), ersetzt keine vorhandenen Releases und verwendet das Repository-`GITHUB_TOKEN` mit Schreibzugriff ausschließlich im Release-Job.

Die Release enthält außerdem automatisch erstellte winget-Manifeste mit den exakten SHA-256-Werten der Archive. Der Workflow übermittelt keine Pakete oder Manifeste an das winget-Community-Repository; die Übermittlung erfolgt separat durch die Maintainer nach Prüfung der Release-Dateien. Nach der Aufnahme des Pakets in winget können Sie es wie folgt installieren:

```powershell
winget install --id eugenioestrada.Wessl --exact
```

Jedes Archiv enthält die vollständige nicht paketierte Veröffentlichungsausgabe. Das winget-Manifest registriert `wessl.exe` als portablen Befehlsalias; Wessl kann auch direkt aus dem entpackten Ordner gestartet werden.

## Verzeichnisstruktur

- `src/Wessl.Domain`: abhängigkeitenfreie Containertypen und Client-Schnittstelle.
- `src/Wessl.Infrastructure`: Beispielclient und asynchroner WSLc-Prozess / JSON-Parser.
- `src/Wessl.App`: Reactor-Komponenten, Infobereich-Lebenszyklus und Abhängigkeitsinjektion.
- `tests/Wessl.Tests`: Domain- und Parser-Tests.
