<p align="center"><img src="NowPlayingWidget/Assets/Logo150.png" width="96" alt="NowPlayingBar logo"></p>

<h1 align="center">NowPlayingBar</h1>

<p align="center">See what's playing right inside the Windows 11 taskbar, with previous, play/pause and next buttons.</p>

<p align="center"><a href="https://github.com/Seb-RS/NowPlayingBar/releases/latest"><b>⬇ Download the latest version</b></a></p>

---

**English** · [Español](#español)

## Features

- **Cover art, title and artist** in the taskbar, with ⏮ ⏯ ⏭ buttons.
- **Music first:** if music and a video (for example, a movie in the browser) play at the same time, the bar shows the music.
- **Pick a source:** right-click → **Source** to choose **Automatic** or always show one app.
- **Smooth song changes:** the previous song stays dimmed until the new title and cover art are ready.
- **Multiple monitors:** right-click → **Screen** to choose a screen or **All screens**. You can also drag it to another taskbar.
- **Horizontal and vertical taskbars.** Drag it to change its position; it remembers where you left it.
- **Starts with Windows** (can be turned off from the menu).
- **Languages:** English, Español, Deutsch, Português (Brasil), Русский. Follows the Windows language; change it in right-click → **Language**.

## Install

1. Download **`NowPlayingBar-Setup-x.y.z.exe`** from [Releases](https://github.com/Seb-RS/NowPlayingBar/releases/latest).
2. Run it. It doesn't need administrator rights or .NET.
3. If Windows shows "Windows protected your PC", click **More info → Run anyway**. This appears because the installer isn't code-signed yet.

To uninstall: Settings → Apps → Installed apps → NowPlayingBar → Uninstall.

## Is it safe?

- It doesn't inject code into any process and doesn't modify Explorer. It only uses public Windows APIs (`GlobalSystemMediaTransportControlsSessionManager`) and a window placed inside the taskbar.
- It doesn't connect to the internet.
- All releases are built by GitHub Actions directly from this source code.

## Build from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```
cd NowPlayingBar
dotnet publish -c Release -o out
```

### Widgets board (experimental)

`NowPlayingWidget` is an optional widget for the Windows Widgets board (Win + W). It requires **Developer Mode** and the Windows App Runtime 1.8:

```
cd NowPlayingWidget
dotnet publish -c Release -r win-x64 -o out
Add-AppxPackage -Register out\AppxManifest.xml
```

Then open the Widgets board → **+** → **Now Playing**.

## Code signing policy


- Committers and reviewers: [Seb-RS](https://github.com/Seb-RS)
- Approvers: [Seb-RS](https://github.com/Seb-RS)

Only binaries built by GitHub Actions from this repository will be signed.

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

NowPlayingBar only reads the media information that Windows exposes locally (title, artist, cover art) and sends play/pause/skip commands to the local media session. Settings are stored locally in `%APPDATA%`.

## Trademarks


## License

[MIT](LICENSE)

---

## Español

**NowPlayingBar** muestra lo que estás escuchando dentro de la barra de tareas de Windows 11, con botones de anterior, pausa y siguiente.

- Portada, título y artista en la barra, con botones ⏮ ⏯ ⏭.
- **La música primero:** si suenan música y un video a la vez, muestra la música.
- Clic derecho → **Fuente** para elegir **Automático** o fijar una app.
- Al cambiar de canción, la anterior se queda atenuada hasta que cargan el título y la portada nuevos.
- Varios monitores (clic derecho → **Pantalla**), barra horizontal o vertical, se puede arrastrar y recuerda su posición.
- Se inicia con Windows (se puede desactivar en el menú).
- Idiomas: español, English, Deutsch, Português (Brasil), Русский.

**Instalación:** descarga **`NowPlayingBar-Setup-x.y.z.exe`** desde [Releases](https://github.com/Seb-RS/NowPlayingBar/releases/latest) y ábrelo. No necesita permisos de administrador. Si aparece "Windows protegió su PC", pulsa **Más información → Ejecutar de todas formas** (sale porque el instalador aún no está firmado).

No inyecta código en ningún proceso ni se conecta a internet.
