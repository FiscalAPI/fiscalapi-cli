# fiscalapi, el CLI de FiscalAPI

`fiscalapi` lleva los webhooks de FiscalAPI a tu terminal: recibe los eventos de tu cuenta en tu equipo y los reenvía a
tu servidor local, dispara eventos de prueba y administra tus webhooks y eventos desde la consola.

## Instalación

Descarga el empaque de tu sistema desde [Releases](https://github.com/FiscalAPI/fiscalapi-cli/releases):

| Sistema             | Empaque                      |
| ------------------- | ---------------------------- |
| Windows x64         | `fiscalapi-win-x64.zip`      |
| Linux x64           | `fiscalapi-linux-x64.tar.gz` |
| macOS Apple Silicon | `fiscalapi-osx-arm64.tar.gz` |
| macOS Intel         | `fiscalapi-osx-x64.tar.gz`   |

Linux (en macOS, cambia el nombre del empaque):

```bash
curl -fsSLO https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download/fiscalapi-linux-x64.tar.gz
tar -xzf fiscalapi-linux-x64.tar.gz
sudo install -m 0755 fiscalapi /usr/local/bin/fiscalapi
fiscalapi --version
```

Windows (PowerShell):

```powershell
$base = 'https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download'
Invoke-WebRequest "$base/fiscalapi-win-x64.zip" -OutFile fiscalapi-win-x64.zip
Expand-Archive fiscalapi-win-x64.zip -DestinationPath (Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi') -Force
```

Si quieres confirmar que la descarga está íntegra, compara su SHA-256 con el archivo `SHA256SUMS` del release:
[Verificar la descarga (opcional)](docs/instalacion.md#verificar-la-descarga-opcional). En
[Instalación](docs/instalacion.md) están los requisitos de cada sistema, cómo agregar `fiscalapi` al `PATH`, actualizar
y desinstalar.

## Autenticación

Cada comando que llama a la API usa tu API key y tu tenant key. Guárdalas una vez en el almacén de credenciales del
sistema con `fiscalapi login`, o defínelas en las variables de entorno `FISCALAPI_API_KEY` y `FISCALAPI_TENANT_KEY`
(por ejemplo, en un CI). El ambiente por omisión es el de pruebas; agrega `--live` para producción. Detalles en
[Autenticación](docs/autenticacion.md).

## Uso

```bash
# 1. Guarda tu API key y tu tenant key del ambiente de pruebas
fiscalapi login

# 2. Recibe los eventos de tu cuenta y reenvíalos a tu servidor local
fiscalapi listen --forward-to http://localhost:3000/webhooks

# 3. En otra terminal, dispara un evento de prueba
fiscalapi trigger invoice.canceled
```

`listen` muestra el secreto de firma de tu sesión y una línea por cada evento que reenvía:

```text
Ready. Your signing secret is whsec_<secreto> (listen session <id>).
2026-10-12 09:47:46  invoice.canceled [3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b] [200]
```

Configura tu servidor con ese secreto de firma para verificar cada entrega
([Verificar la firma](docs/listen.md#verificar-la-firma)).

## Comandos

| Comando                                                   | Qué hace                                                     | Guía                                             |
| --------------------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------------ |
| `fiscalapi login`, `fiscalapi logout`                     | Guarda o elimina tus credenciales de un ambiente.            | [Autenticación](docs/autenticacion.md)           |
| `fiscalapi listen`                                        | Recibe los eventos en tu equipo y los reenvía a tu servidor. | [`fiscalapi listen`](docs/listen.md)             |
| `fiscalapi trigger <type>`                                | Dispara un evento de prueba. Solo en el ambiente de pruebas. | [`fiscalapi trigger`](docs/trigger.md)           |
| `fiscalapi webhooks list`, `retrieve`, `create`, `update` | Lista, muestra, crea y actualiza tus webhooks.               | [Webhooks y eventos](docs/webhooks-y-eventos.md) |
| `fiscalapi webhooks delete`, `rotate-secret`              | Elimina un webhook o genera un secreto de firma nuevo.       | [Webhooks y eventos](docs/webhooks-y-eventos.md) |
| `fiscalapi events list`, `retrieve`, `resend`             | Lista, muestra y reenvía los eventos de los últimos 30 días. | [Webhooks y eventos](docs/webhooks-y-eventos.md) |

Las opciones globales `--live`, `--api-base`, `--json` y `--verbose`, y los códigos de salida, están en
[Salida y códigos](docs/salida-y-codigos.md). `fiscalapi --help` y `fiscalapi <comando> --help` describen cada opción.

La guía de webhooks de FiscalAPI (forma del evento, firma, reintentos y rotación del secreto de firma) está en
[docs.fiscalapi.com/webhooks-info](https://docs.fiscalapi.com/webhooks-info).

## Solución de problemas

Los errores frecuentes, con su causa y remedio, están en [Solución de problemas](docs/solucion-de-problemas.md).

## Licencia

Este proyecto se distribuye bajo la Mozilla Public License 2.0. Ver [LICENSE](LICENSE).
