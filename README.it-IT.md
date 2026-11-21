# Wessl

[English](README.md) · [Español](README.es-ES.md) · [简体中文](README.zh-CN.md) · [Français](README.fr-FR.md) · [Italiano](README.it-IT.md) · [Deutsch](README.de-DE.md)

Wessl è un’applicazione nativa leggera per l’area di notifica di Windows, dedicata a WSL Containers (WSLc) e realizzata con Microsoft.UI.Reactor e WinUI 3. Si avvia nell’area di notifica: fai clic con il pulsante sinistro sull’icona per aprire la finestra dei contenitori accanto all’area oppure fai clic con il pulsante destro per visualizzare il menu WinUI nativo con le voci Apri ed Esci.

Quando viene aperto dall’area di notifica, il pannello dei contenitori appare e riceve lo stato attivo con una breve dissolvenza in entrata. Quando lo stato attivo passa a un’altra applicazione o finestra degli strumenti, il pannello scompare con una dissolvenza mentre l’app rimane nell’area di notifica. Riaprendo il pannello si annulla qualsiasi dissolvenza in uscita in sospeso. Il pannello non rimane sempre in primo piano e le transizioni rispettano l’impostazione delle animazioni di Windows.

Consulta la [roadmap](ROADMAP.md) per i traguardi pianificati e le priorità delle funzionalità.

## Perché “Wessl”?

**Wessl** è una parola composta da **WSL** e **Vessel**, un termine che indica sia una nave che trasporta contenitori sia un contenitore stesso. Mantiene il richiamo nautico dell’ecosistema dei contenitori senza ricorrere ai soliti cliché. Si pronuncia come *vessel* (con la *W*), in linea con la filosofia di design breve, pulita e leggera dell’app.

Per impostazione predefinita, l’app usa il client WSLc reale. L’eseguibile `wslc` deve essere disponibile in `PATH` e il comando di elenco deve supportare `wslc container list --all --format json`, con un oggetto JSON per riga. Se non è possibile avviare WSLc, l’app mostra istruzioni per aggiornare WSL e un pulsante per copiare `wsl --update`; non usa dati di esempio come ripiego. Per usare dati di esempio di contenitori e immagini a scopo dimostrativo, imposta `WESSL_USE_SAMPLE_WSLC=1` prima di avviare l’app. Le operazioni Avvia, Arresta e Riavvia sono disponibili direttamente in ogni riga; la rimozione richiede conferma e può arrestare un contenitore in esecuzione prima di eliminarlo.

Le mappature TCP pubblicate nel campo `Ports` di un contenitore offrono anche URL localhost che possono essere aperti o copiati.

Apri **Immagini** per caricare esplicitamente l’elenco delle immagini WSLc e le dimensioni indicate. Aggiorna ricarica l’elenco. **Rimuovi immagini inutilizzate** richiede conferma ed elimina tutte le immagini non referenziate da alcun contenitore, comprese quelle con tag; vengono mostrati il risultato di WSLc e gli eventuali errori. Le operazioni sulle immagini possono avviare WSL e vengono eseguite solo dopo un’azione dell’utente.

Per un contenitore in esecuzione, **Apri terminale** avvia Windows Terminal con una sessione interattiva `wslc exec -it <container-id> /bin/sh`. Questa operazione richiede l’integrazione WSLc reale; devono essere disponibili Windows Terminal e una versione di WSLc che supporti `exec -it`.

**Visualizza log** apre una finestra separata che segue `wslc container logs -f <container-id>`. Chiudendo la finestra si arresta il processo di monitoraggio. La visualizzazione conserva gli ultimi 128 Ki caratteri.

Lo stato dei contenitori viene aggiornato all’apertura della finestra e ogni 10 secondi mentre è visibile. Il pulsante Aggiorna richiede anche un aggiornamento immediato. Il polling si interrompe quando la finestra viene nascosta, ridotta a icona o chiusa; avviare l’app nell’area di notifica non invoca WSLc. L’indicatore nell’area mostra uno stato sconosciuto fino al primo aggiornamento riuscito, quindi riflette l’ultimo stato noto mentre la finestra è chiusa.

## Prerequisiti

- Windows 10 versione 2004 (build 19041) o successiva, x64 o ARM64.
- .NET 11 SDK e Windows SDK / strumenti di compilazione WinUI (ad esempio Visual Studio con i carichi di lavoro Sviluppo desktop .NET e Sviluppo di applicazioni Windows).
- Per l’integrazione reale: WSL con WSL Containers e `wslc` in `PATH`. Consulta la [guida a WSL Containers](https://learn.microsoft.com/en-us/windows/wsl/tutorials/wsl-containers).

## Compilazione ed esecuzione

Da un terminale di sviluppo Windows, nella radice del repository:

```powershell
dotnet restore Wessl.slnx
dotnet build Wessl.slnx
dotnet run --project src/Wessl.App/Wessl.App.csproj
dotnet test tests/Wessl.Tests/Wessl.Tests.csproj
```

Il file `NuGet.Config` del repository ripristina i pacchetti da nuget.org, indipendentemente da eventuali altre origini configurate nel computer.

Per ARM64, passa `-p:RuntimeIdentifier=win-arm64` ai comandi di ripristino, compilazione ed esecuzione invece del valore predefinito `win-x64`. L’app non è pacchettizzata e include il runtime di Windows App SDK. La compilazione dell’app WinUI richiede Windows; i test Domain e Infrastructure possono essere eseguiti su qualsiasi piattaforma supportata da .NET 11.

## Localizzazione

L’app WinUI usa le risorse `.resw` MRT Core di Windows App SDK in `src/Wessl.App/Strings/<BCP-47>/Resources.resw`. Sono supportati e selezionabili nelle impostazioni: inglese (`en-US`), spagnolo (`es-ES`), cinese semplificato (`zh-CN`), francese (`fr-FR`), italiano (`it-IT`) e tedesco (`de-DE`). Per aggiungere una traduzione, crea una cartella per la nuova lingua con le stesse chiavi di risorsa; la compilazione di Windows App SDK indicizza questi file nelle risorse PRI dell’app.

## Pubblicazione NativeAOT

Sono disponibili pubblicazioni NativeAOT non pacchettizzate per Windows x64 e ARM64. Esegui i seguenti comandi da Windows dopo aver installato .NET 11 SDK e Visual C++ Build Tools (con il carico di lavoro Sviluppo desktop con C++):

```powershell
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-x64-nativeaot
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-arm64-nativeaot
```

Gli artefatti vengono salvati in:

- `src/Wessl.App/bin/publish/win-x64-nativeaot/`
- `src/Wessl.App/bin/publish/win-arm64-nativeaot/`

Ogni artefatto contiene `wessl.exe`. Le compilazioni NativeAOT disabilitano i dati di globalizzazione tramite `InvariantGlobalization=true` e non supportano reflection o codice dinamico al di fuori della superficie del framework inclusa nella compilazione AOT. L’app evita attualmente codice che fa ampio uso della reflection e `Reactor.DevtoolsSupport` non è abilitato nelle compilazioni Release. La pubblicazione non richiede modifiche al normale flusso di compilazione o esecuzione.

La pubblicazione NativeAOT richiede di specificare l’architettura Windows corrispondente. La pubblicazione incrociata ARM64 da Windows x64 richiede i relativi strumenti di cross-compilazione Visual C++ e non è previsto che l’app venga eseguita sul computer usato per la pubblicazione.

## Distribuzione delle release

I workflow GitHub Actions compilano e testano le pull request e i push a `main`. Il push di un tag di versione, ad esempio `v1.2.3`, avvia il workflow di release, che pubblica archivi NativeAOT per `win-x64` e `win-arm64` e crea una GitHub Release con note generate. Il workflow rifiuta tag che non rispettano il formato `vMAJOR.MINOR.PATCH` (è consentito un suffisso facoltativo di pre-release), non sostituisce release esistenti e usa il `GITHUB_TOKEN` fornito dal repository con autorizzazioni di scrittura solo nel job di release.

La release include anche un insieme generato di manifest winget con i valori SHA-256 esatti degli archivi. Il workflow non invia pacchetti o manifest al repository della community winget: l’invio è un’attività separata di manutenzione, da eseguire dopo aver esaminato gli artefatti. Dopo l’accettazione del pacchetto in winget, gli utenti possono installarlo con:

```powershell
winget install --id eugenioestrada.Wessl --exact
```

Ogni archivio contiene l’intero output di pubblicazione non pacchettizzato. Il manifest winget registra `wessl.exe` come alias di comando portatile; Wessl può anche essere avviato direttamente dalla cartella estratta.

## Struttura

- `src/Wessl.Domain`: tipi di contenitore senza dipendenze e interfaccia del client.
- `src/Wessl.Infrastructure`: client di esempio e processo WSLc asincrono / parser JSON.
- `src/Wessl.App`: componenti Reactor, ciclo di vita dell’area di notifica e dependency injection.
- `tests/Wessl.Tests`: test del dominio e del parser.
