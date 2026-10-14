# fiscalapi — CLI de FiscalAPI

`fiscalapi` lleva los webhooks de FiscalAPI a tu terminal: recibe los eventos en tu equipo y los reenvía a tu servidor
local, dispara eventos de prueba y administra tus webhooks y eventos sin salir de la consola.

Es un solo binario nativo para Windows, Linux y macOS: no necesitas instalar .NET.

## Instalación

Descarga el empaque de tu sistema y `SHA256SUMS` desde
[Releases](https://github.com/FiscalAPI/fiscalapi-cli/releases), y **verifica el SHA-256 antes de ejecutarlo**:

| Sistema | Empaque |
|---|---|
| Windows x64 | `fiscalapi-win-x64.zip` |
| Linux x64 (glibc 2.28+) | `fiscalapi-linux-x64.tar.gz` |
| macOS Apple Silicon | `fiscalapi-osx-arm64.tar.gz` |
| macOS Intel | `fiscalapi-osx-x64.tar.gz` |

Linux (en macOS, cambia el empaque y usa `shasum -a 256 -c -` en lugar de `sha256sum -c -`):

```bash
base=https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download
curl -fsSLO "$base/fiscalapi-linux-x64.tar.gz"
curl -fsSLO "$base/SHA256SUMS"
grep ' fiscalapi-linux-x64.tar.gz$' SHA256SUMS | sha256sum -c -   # debe decir: OK
tar -xzf fiscalapi-linux-x64.tar.gz
sudo install -m 0755 fiscalapi /usr/local/bin/fiscalapi
```

Windows (PowerShell):

```powershell
$base = 'https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download'
Invoke-WebRequest "$base/fiscalapi-win-x64.zip" -OutFile fiscalapi-win-x64.zip
Invoke-WebRequest "$base/SHA256SUMS" -OutFile SHA256SUMS
$expected = (Select-String -Path SHA256SUMS -Pattern ' fiscalapi-win-x64\.zip$').Line.Split(' ')[0]
$actual = (Get-FileHash fiscalapi-win-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw "El SHA-256 no coincide: $actual" } else { 'SHA-256 OK' }
Expand-Archive fiscalapi-win-x64.zip -DestinationPath (Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi') -Force
```

Cómo agregarlo al `PATH`, Gatekeeper y SmartScreen, actualizar, el aviso de versión nueva y desinstalar:
[docs/instalacion.md](docs/instalacion.md).

## Inicio rápido

```bash
# 1. Guarda tu API key y tu tenant key en el almacén de credenciales del sistema (ambiente de pruebas)
fiscalapi login

# 2. Recibe los eventos de tu cuenta y reenvíalos a tu servidor local
fiscalapi listen --forward-to http://localhost:3000/webhooks
#    Listo. Tu secreto de firma es whsec_… (escucha …)
#    2026-10-12 09:47:46  --> invoice.canceled [evt_0001] [200]

# 3. En otra terminal, dispara un evento de prueba hacia tu escucha
fiscalapi trigger invoice.canceled
```

Verifica la firma de cada entrega en tu servidor con el secreto `whsec_…` que muestra `listen` y el `Verify` del SDK de
tu lenguaje ([cómo](docs/listen.md#verificar-la-firma-en-tu-servidor-local)). `fiscalapi trigger` solo funciona en el
ambiente de pruebas ([detalles](docs/trigger.md)).

## Documentación

| Tema | Contenido |
|---|---|
| [Instalación](docs/instalacion.md) | Binarios por sistema, verificación, `PATH`, actualización, aviso de versión nueva y desinstalación. |
| [Autenticación](docs/autenticacion.md) | `login`/`logout`, almacén de credenciales, variables de entorno, ambientes y CI. |
| [`fiscalapi listen`](docs/listen.md) | Recibir eventos en local: opciones, firma, reconexión y salida `--json`. |
| [Webhooks y eventos](docs/webhooks-y-eventos.md) | `webhooks list/retrieve/create/update/delete/rotate-secret` y `events list/retrieve/resend`. |
| [`fiscalapi trigger`](docs/trigger.md) | Eventos de prueba hacia tu escucha o un webhook: tipos, opciones y errores. |
| [Salida y códigos](docs/salida-y-codigos.md) | stdout/stderr, `--json`, `--verbose` y códigos de salida. |
| [Solución de problemas](docs/solucion-de-problemas.md) | Errores frecuentes con su causa y remedio. |

`fiscalapi --help` y `fiscalapi <comando> --help` describen cada opción. La guía de webhooks de la API (forma del
evento, firma, reintentos) está en [docs.fiscalapi.com/webhooks](https://docs.fiscalapi.com/webhooks).

## Desarrollo

Requiere el SDK de .NET de `global.json`. Para el binario nativo, los prerrequisitos de
[Native AOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/): en Windows, Visual Studio con
"Desktop development with C++"; en Linux, `clang` y `zlib1g-dev`; en macOS, las Command Line Tools de Xcode.

```bash
dotnet build FiscalApi.Cli.slnx -c Release
dotnet test FiscalApi.Cli.slnx -c Release
dotnet publish src/FiscalApi.Cli -c Release -r <rid>    # win-x64, linux-x64, osx-arm64 u osx-x64
```

Con Visual Studio 2026, si el publish de Windows falla con "'vswhere.exe' is not recognized", agrega
`C:\Program Files (x86)\Microsoft Visual Studio\Installer` al PATH.

### Releases

`.github/workflows/release.yml` compila cada binario en un runner de su sistema (AOT no compila entre sistemas):
Windows, Linux dentro de AlmaLinux 8 (para que el binario corra desde glibc 2.28), macOS Apple Silicon y macOS Intel.
Corre las pruebas del almacén de credenciales en Windows y macOS, prueba cada binario con `--version` y deja un
release **en borrador** con los 4 empaques y `SHA256SUMS`. Publicarlo es una decisión manual.

- Release real: empuja un tag `v1.2.3`.
- Prueba sin tag: `gh workflow run release.yml -R FiscalAPI/fiscalapi-cli --ref <rama> -f version=0.1.0-rc.1`. Deja un
  borrador marcado como prerelease; bórralo con `gh release delete v0.1.0-rc.1 -R FiscalAPI/fiscalapi-cli --yes`.
