# Wessl

[English](README.md) · [Español](README.es-ES.md) · [简体中文](README.zh-CN.md) · [Français](README.fr-FR.md) · [Italiano](README.it-IT.md) · [Deutsch](README.de-DE.md)

Wessl es una aplicación nativa y ligera para la bandeja del sistema de Windows, diseñada para WSL Containers (WSLc) con Microsoft.UI.Reactor y WinUI 3. Se inicia en la bandeja: haz clic con el botón izquierdo en el icono para abrir la ventana de contenedores junto a la bandeja, o con el botón derecho para abrir el menú nativo de WinUI con las opciones Abrir y Salir.

Al abrirla desde la bandeja, el panel aparece enfocado con una breve animación de entrada. Cuando el foco pasa a otra aplicación o ventana de herramientas, el panel se desvanece y se oculta, pero la aplicación sigue activa en la bandeja. Al volver a abrirlo se cancela cualquier animación de cierre pendiente. El panel no permanece siempre encima y las transiciones respetan la configuración de animaciones de Windows.

Consulta la [hoja de ruta](ROADMAP.md) para ver los hitos y prioridades previstos.

## ¿Por qué «Wessl»?

**Wessl** combina **WSL** y **Vessel**, una palabra que significa tanto una embarcación que transporta contenedores como un recipiente. Conserva la alusión náutica del ecosistema de contenedores sin recurrir a los clichés habituales. Se pronuncia como *vessel* (con una *W*), en sintonía con la filosofía de diseño breve, clara y ligera de la aplicación.

La aplicación usa el cliente WSLc real de forma predeterminada. El ejecutable `wslc` debe estar en `PATH`, y su comando de lista debe admitir `wslc container list --all --format json`, con un objeto JSON por línea. Si no se puede iniciar WSLc, la aplicación muestra instrucciones para actualizar WSL y un botón para copiar `wsl --update`; no recurre a datos de ejemplo. Para usar datos de ejemplo de contenedores e imágenes en una demostración, define `WESSL_USE_SAMPLE_WSLC=1` antes de iniciar la aplicación. Iniciar, detener y reiniciar se ejecutan directamente desde cada fila; para eliminar un contenedor se pide confirmación y, si está en ejecución, se puede detener como parte de la eliminación.

Las asignaciones TCP publicadas en el campo `Ports` de un contenedor también ofrecen URL de localhost que se pueden abrir o copiar.

Abre **Imágenes** para cargar explícitamente la lista de imágenes de WSLc y el tamaño indicado. Actualizar vuelve a cargarla. **Limpiar imágenes sin usar** pide confirmación y elimina todas las imágenes que no estén referenciadas por ningún contenedor, incluidas las que tengan etiquetas; se muestran el resultado de WSLc y los posibles errores. Las operaciones con imágenes pueden iniciar WSL y solo se ejecutan tras una acción del usuario.

Para un contenedor en ejecución, **Abrir consola** inicia Windows Terminal con una sesión interactiva `wslc exec -it <container-id> /bin/sh`. Esta acción requiere la integración real con WSLc; Windows Terminal y una versión de WSLc compatible con `exec -it` deben estar disponibles.

**Ver registros** abre una ventana independiente que sigue `wslc container logs -f <container-id>`. Al cerrar la ventana se detiene el proceso de seguimiento. La vista conserva los 128 Ki más recientes.

Para un contenedor en ejecución, **Mostrar uso de CPU y RAM** solicita una instantánea con `wslc stats --format json <container-id>`. El uso no se sondea automáticamente.

El estado de los contenedores se actualiza al abrir la ventana y cada 10 segundos mientras permanece visible. El botón Actualizar también solicita una actualización inmediata. El sondeo se detiene cuando la ventana se oculta, minimiza o cierra; iniciar la aplicación en la bandeja no invoca WSLc. El indicador de la bandeja muestra un estado desconocido hasta la primera actualización correcta y, después, refleja el último estado conocido mientras la ventana está cerrada.

## Requisitos previos

- Windows 10, versión 2004 (compilación 19041) o posterior, x64 o ARM64.
- .NET 11 SDK y Windows SDK / herramientas de compilación de WinUI (por ejemplo, Visual Studio con las cargas de trabajo de desarrollo de escritorio de .NET y desarrollo de aplicaciones de Windows).
- Para la integración real: WSL con WSL Containers y `wslc` en `PATH`. Consulta la [guía de WSL Containers](https://learn.microsoft.com/en-us/windows/wsl/tutorials/wsl-containers).

## Compilar y ejecutar

Desde un terminal de desarrollo de Windows en la raíz del repositorio:

```powershell
dotnet restore Wessl.slnx
dotnet build Wessl.slnx
dotnet run --project src/Wessl.App/Wessl.App.csproj
dotnet test tests/Wessl.Tests/Wessl.Tests.csproj
```

El archivo `NuGet.Config` del repositorio restaura paquetes desde nuget.org, independientemente de otros orígenes de paquetes configurados en el equipo.

Para ARM64, pasa `-p:RuntimeIdentifier=win-arm64` a los comandos de restauración, compilación y ejecución en lugar del `win-x64` predeterminado. La aplicación no está empaquetada e incluye el runtime de Windows App SDK. La compilación de la aplicación WinUI requiere Windows; las pruebas de Domain e Infrastructure pueden ejecutarse en cualquier plataforma compatible con .NET 11.

## Localización

La aplicación WinUI usa recursos `.resw` de MRT Core del Windows App SDK en `src/Wessl.App/Strings/<BCP-47>/Resources.resw`. Se admiten inglés (`en-US`), español (`es-ES`), chino simplificado (`zh-CN`), francés (`fr-FR`), italiano (`it-IT`) y alemán (`de-DE`), seleccionables desde la configuración. Para añadir un idioma, crea una carpeta nueva con las mismas claves de recursos; la compilación del Windows App SDK indexa esos archivos en los recursos PRI de la aplicación.

## Publicación con NativeAOT

Se pueden publicar compilaciones NativeAOT sin empaquetar para Windows x64 y ARM64. Ejecuta estos comandos en Windows con .NET 11 SDK y Visual C++ Build Tools (con la carga de trabajo Desarrollo para el escritorio con C++) instalados:

```powershell
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-x64-nativeaot
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-arm64-nativeaot
```

Los artefactos se guardan en:

- `src/Wessl.App/bin/publish/win-x64-nativeaot/`
- `src/Wessl.App/bin/publish/win-arm64-nativeaot/`

Cada artefacto contiene `wessl.exe`. Las compilaciones NativeAOT desactivan los datos de globalización mediante `InvariantGlobalization=true` y no admiten reflexión ni código dinámico fuera de la superficie del framework incluida en la compilación AOT. Actualmente, la aplicación evita código que dependa mucho de la reflexión y `Reactor.DevtoolsSupport` no se habilita en compilaciones Release. La publicación no requiere cambios en el flujo habitual de compilación o ejecución.

La publicación NativeAOT requiere compilar para la arquitectura de Windows correspondiente. Para publicar ARM64 desde Windows x64 se necesitan las herramientas de compilación cruzada de Visual C++ correspondientes; no se espera que el resultado se ejecute en el equipo donde se publicó.

## Distribución de versiones

Los flujos de GitHub Actions compilan y prueban las solicitudes de incorporación de cambios y las inserciones en `main`. Al insertar una etiqueta de versión, como `v1.2.3`, se inicia el flujo de publicación: genera archivos NativeAOT para `win-x64` y `win-arm64` y crea una versión de GitHub con notas generadas. El flujo rechaza etiquetas que no sigan el formato `vMAJOR.MINOR.PATCH` (se permite un sufijo de versión preliminar opcional), no sobrescribe versiones existentes y usa el `GITHUB_TOKEN` del repositorio con permisos de escritura únicamente en el trabajo de publicación.

La versión también incluye un conjunto generado de manifiestos de winget con los valores SHA-256 exactos de los archivos. El flujo no envía paquetes ni manifiestos al repositorio comunitario de winget; esa acción corresponde por separado a una persona mantenedora, después de revisar los artefactos. Una vez que se acepte el paquete en winget, se puede instalar con:

```powershell
winget install --id eugenioestrada.Wessl --exact
```

Cada archivo contiene toda la salida de publicación sin empaquetar. El manifiesto de winget registra `wessl.exe` como alias de comando portátil; también se puede iniciar Wessl directamente desde la carpeta extraída.

## Estructura

- `src/Wessl.Domain`: tipos de contenedor sin dependencias e interfaz de cliente.
- `src/Wessl.Infrastructure`: cliente de ejemplo y proceso WSLc asíncrono / analizador JSON.
- `src/Wessl.App`: componentes Reactor, ciclo de vida de la bandeja e inyección de dependencias.
- `tests/Wessl.Tests`: pruebas de Domain y del analizador.
