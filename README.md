# NowPlayingBar


Incluye además un widget para el panel de Widgets de Windows (Win + W).

## NowPlayingBar (barra de tareas)

- Portada, título y artista dentro de la barra de tareas.
- Botones ⏮ ⏯ ⏭.
- Se puede arrastrar para cambiar su posición.
- Varios monitores: clic derecho → **Pantalla**, o arrástralo hasta la barra de otra pantalla.
- Recuerda la posición y la pantalla (`%APPDATA%\NowPlayingBar.txt`).
- No inyecta código en ningún proceso. Usa solo las APIs públicas de Windows (`GlobalSystemMediaTransportControlsSessionManager`) y una ventana hija de la barra de tareas.

### Compilar

Requiere el [SDK de .NET 9](https://dotnet.microsoft.com/download).

```
cd NowPlayingBar
dotnet publish -c Release -o out
```

Ejecuta `out\NowPlayingBar.exe`. Para que arranque con Windows, crea un acceso directo en `shell:startup`.

## NowPlayingWidget (panel de Widgets)

Proveedor de widget hecho con Windows App SDK 1.8 (tarjeta adaptativa con portada y controles).

Requisitos:
- Windows 11 con el **Modo de desarrollador** activado.
- Windows App Runtime 1.8 instalado.

```
cd NowPlayingWidget
dotnet publish -c Release -r win-x64 -o out
Add-AppxPackage -Register out\AppxManifest.xml
```


## Licencia

MIT
