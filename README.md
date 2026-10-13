# fiscalapi — CLI de FiscalAPI

`fiscalapi` lleva los webhooks de FiscalAPI a tu terminal: recibe los eventos en tu equipo y los reenvía a tu servidor
local, dispara eventos de prueba y administra tus webhooks y eventos sin salir de la consola.

Está inspirado en la Stripe CLI: [`stripe listen`](https://docs.stripe.com/cli/listen),
[`stripe trigger`](https://docs.stripe.com/cli/trigger) y [`stripe events resend`](https://docs.stripe.com/cli/events/resend)
tienen aquí su equivalente. [`stripe logs tail`](https://docs.stripe.com/cli/logs/tail) no lo tiene, porque FiscalAPI no
guarda un log de peticiones por cuenta.

Es un solo binario nativo (.NET Native AOT): no necesitas instalar .NET.

## Instalación

Cada [release](https://github.com/FiscalAPI/fiscalapi-cli/releases) trae un empaque por sistema y un archivo
`SHA256SUMS` con el SHA-256 de cada uno. **Verifica el SHA-256 antes de ejecutar el binario.**

| Sistema | Empaque |
|---|---|
| Windows x64 | `fiscalapi-win-x64.zip` |
| Linux x64 (glibc 2.28 o más nueva: RHEL/AlmaLinux/Rocky 8+, Debian 10+, Ubuntu 20.04+) | `fiscalapi-linux-x64.tar.gz` |
| macOS Apple Silicon | `fiscalapi-osx-arm64.tar.gz` |
| macOS Intel | `fiscalapi-osx-x64.tar.gz` |

Los comandos de abajo descargan la última versión. Para una versión fija, cambia `releases/latest/download` por
`releases/download/v1.2.3`.

### Linux

```bash
base=https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download
curl -fsSLO "$base/fiscalapi-linux-x64.tar.gz"
curl -fsSLO "$base/SHA256SUMS"
grep ' fiscalapi-linux-x64.tar.gz$' SHA256SUMS | sha256sum -c -   # debe decir: OK
tar -xzf fiscalapi-linux-x64.tar.gz
sudo install -m 0755 fiscalapi /usr/local/bin/fiscalapi
fiscalapi --version
```

### macOS

En una Mac con Apple Silicon usa `fiscalapi-osx-arm64.tar.gz`; en una Intel, `fiscalapi-osx-x64.tar.gz`.

```bash
package=fiscalapi-osx-arm64.tar.gz
base=https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download
curl -fsSLO "$base/$package"
curl -fsSLO "$base/SHA256SUMS"
grep " $package\$" SHA256SUMS | shasum -a 256 -c -   # debe decir: OK
tar -xzf "$package"
sudo mkdir -p /usr/local/bin
sudo install -m 0755 fiscalapi /usr/local/bin/fiscalapi
fiscalapi --version
```

El binario no está firmado ni notarizado por Apple. Descargado con `curl` corre sin más; si lo bajaste con el
navegador, macOS lo bloquea la primera vez: quítale la marca de cuarentena con
`xattr -d com.apple.quarantine /usr/local/bin/fiscalapi`.

### Windows (PowerShell)

```powershell
$base = 'https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download'
Invoke-WebRequest "$base/fiscalapi-win-x64.zip" -OutFile fiscalapi-win-x64.zip
Invoke-WebRequest "$base/SHA256SUMS" -OutFile SHA256SUMS
$expected = (Select-String -Path SHA256SUMS -Pattern ' fiscalapi-win-x64\.zip$').Line.Split(' ')[0]
$actual = (Get-FileHash fiscalapi-win-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw "El SHA-256 no coincide: $actual" } else { 'SHA-256 OK' }

$dir = Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi'
Expand-Archive fiscalapi-win-x64.zip -DestinationPath $dir -Force

# Agrega la carpeta al PATH de tu usuario sin expandir las variables que ya tenga (%USERPROFILE%…).
$environment = Get-Item 'HKCU:\Environment'
$userPath = $environment.GetValue('Path', '', 'DoNotExpandEnvironmentNames')
if (($userPath -split ';') -notcontains $dir) {
    $newPath = (@($userPath.TrimEnd(';'), $dir) | Where-Object { $_ }) -join ';'
    Set-ItemProperty -Path 'HKCU:\Environment' -Name Path -Value $newPath -Type ExpandString
    # Avisa a Windows del cambio para que las terminales nuevas lo vean (no crea ninguna variable).
    [Environment]::SetEnvironmentVariable('FISCALAPI_CLI_PATH_REFRESH', [NullString]::Value, 'User')
}
```

Si prefieres hacerlo a mano: "Editar las variables de entorno de esta cuenta" → `Path` → Nuevo → la carpeta
`%LOCALAPPDATA%\Programs\fiscalapi`.

Abre una terminal nueva y corre `fiscalapi --version`. El binario no lleva firma Authenticode: si lo bajaste con el
navegador, SmartScreen puede advertirlo; `Unblock-File "$dir\fiscalapi.exe"` quita la marca de descarga.

## Primeros pasos

### `login` y `logout`

```bash
fiscalapi login            # ambiente de pruebas (https://test.fiscalapi.com)
fiscalapi login --live     # producción (https://live.fiscalapi.com)
```

`login` pide tu API key y tu tenant key sin mostrarlas, las valida contra la API y las guarda en el almacén de
credenciales del sistema (Credential Manager en Windows, el llavero en macOS, Secret Service/libsecret en Linux), una
entrada por ambiente. Nunca se aceptan como argumento. `fiscalapi logout` las borra.

Sin terminal (un script o un CI), pásalas por stdin, una por línea, o usa las variables de entorno:

```bash
printf '%s\n%s\n' "$API_KEY" "$TENANT_KEY" | fiscalapi login
```

En Linux sin Secret Service (WSL, un servidor o un contenedor) no hay dónde guardarlas: define `FISCALAPI_API_KEY` y
`FISCALAPI_TENANT_KEY`. No hay respaldo en un archivo de texto.

> **Terminales sin ConPTY.** En mintty (la terminal de Git Bash en Windows) y en `docker run -i` sin `-t`, la entrada
> no llega como una terminal: `login` la trata como redirigida, sin prompt y **mostrando lo que escribes**. Usa Windows
> Terminal o PowerShell, `docker run -it`, o las variables de entorno.

### Recibir webhooks en tu equipo: `listen` + `trigger`

```bash
fiscalapi listen --forward-to http://localhost:3000/webhooks
```

`listen` abre una escucha y muestra `Listo. Tu secreto de firma es whsec_… (escucha …)`. Usa ese secreto en tu
servidor para verificar la firma: cada reenvío lleva el cuerpo exacto y los headers `webhook-id`, `webhook-timestamp` y
`webhook-signature` que firmó FiscalAPI (Standard Webhooks). Por cada evento verás una línea como:

```
2026-10-12 09:47:46  --> invoice.canceled [evt_…] [200]
```

En otra terminal, dispara un evento de prueba hacia tu escucha:

```bash
fiscalapi trigger invoice.canceled
```

Los tipos son `invoice.cancellation_requested`, `invoice.canceled`, `invoice.cancellation_rejected`,
`download_request.completed`, `download_request.abandoned` y `download_rule.suspended`. `trigger` solo funciona en el
ambiente de pruebas; en producción la API responde 403.

Opciones de `listen`:

| Opción | Uso |
|---|---|
| `--forward-to <url>` | URL de tu servidor local. Sin ella, `listen` solo muestra los eventos. |
| `--events <tipos>` | Tipos separados por coma; por omisión, todos. |
| `--headers "K:V,K:V"` | Headers extra en cada reenvío. |
| `--skip-verify` | No verifica el certificado TLS de `--forward-to` (p. ej. uno autofirmado). Nunca afecta a la API. |
| `--print-secret` | Imprime solo el secreto de firma y termina. |

Ctrl+C cierra la escucha.

### Webhooks y eventos

```bash
fiscalapi webhooks list
fiscalapi webhooks create --url https://mi-servidor.com/webhooks --events invoice.canceled,invoice.cancellation_rejected
fiscalapi webhooks retrieve <id>
fiscalapi webhooks update <id> --disabled
fiscalapi webhooks rotate-secret <id>
fiscalapi webhooks delete <id>

fiscalapi events list --type invoice.canceled --from 2026-10-01
fiscalapi events retrieve <eventId>
fiscalapi events resend <eventId>                     # a tu escucha de fiscalapi listen
fiscalapi events resend <eventId> --webhook <id>      # a un webhook
```

`create` y `rotate-secret` muestran el secreto de firma **una sola vez**: guárdalo. `fiscalapi <comando> --help`
describe cada opción.

## Opciones globales

| Opción | Uso |
|---|---|
| `--live` | Usa producción (`https://live.fiscalapi.com`) en lugar de pruebas. |
| `--api-base <url>` | URL base de la API (p. ej. un back local). Gana sobre `--live`. Con `http` solo se acepta localhost. |
| `--json` | Solo JSON en stdout, para scripts. Los mensajes y errores van a stderr. |
| `--verbose` | Detalle HTTP en stderr, sin los headers de credenciales. |
| `--version` | Versión del CLI. |

## Variables de entorno

| Variable | Efecto |
|---|---|
| `FISCALAPI_API_KEY` y `FISCALAPI_TENANT_KEY` | Credenciales. Se definen juntas y ganan sobre las que guardó `login`. |
| `FISCALAPI_NO_UPDATE_CHECK` | Con `1` (o `true`) apaga el aviso de versión nueva. |
| `CI` | Con `true` (o `1`), como en GitHub Actions y la mayoría de los CI, también apaga el aviso. |

## Aviso de versión nueva

Una vez cada 24 horas como mucho, `fiscalapi` pregunta a GitHub por el último release publicado (sin enviar tus
credenciales) mientras corre tu comando. Si hay uno más nuevo, al final escribe en stderr la versión y la liga de
descarga. La consulta tarda como mucho 2 segundos y, si falla, no muestra nada ni cambia el resultado del comando.

No corre con `--json`, con stderr redirigido, con `CI=true` ni con `FISCALAPI_NO_UPDATE_CHECK=1`. La fecha de la última
consulta, sin ningún secreto, se guarda en:

| Sistema | Archivo |
|---|---|
| Windows | `%LOCALAPPDATA%\fiscalapi-cli\update-check.json` |
| macOS | `~/Library/Application Support/fiscalapi-cli/update-check.json` |
| Linux | `$XDG_STATE_HOME/fiscalapi-cli/update-check.json` (por omisión `~/.local/state/…`) |

## Códigos de salida

| Código | Caso |
|---|---|
| 0 | Éxito. |
| 1 | Error de la API o de la red (el mensaje incluye el `traceIdentifier` para soporte), o almacén de credenciales bloqueado. |
| 2 | Error de uso: opción o argumento inválido, o falta de credenciales o de almacén. |
| 130 | Cancelado con Ctrl+C. |

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
Prueba cada binario con `--version`, corre en macOS las pruebas del llavero y deja un release **en borrador** con los
4 empaques y `SHA256SUMS`. Publicarlo es una decisión manual.

- Release real: empuja un tag `v1.2.3`.
- Prueba sin tag: `gh workflow run release.yml -R FiscalAPI/fiscalapi-cli --ref <rama> -f version=0.1.0-rc.1`. Deja un
  borrador marcado como prerelease; bórralo con `gh release delete v0.1.0-rc.1 -R FiscalAPI/fiscalapi-cli --yes`.
