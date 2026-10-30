# Salida y códigos de salida

## Opciones globales

Todos los comandos aceptan:

| Opción             | Descripción                                                                                              |
| ------------------ | -------------------------------------------------------------------------------------------------------- |
| `--live`           | Usa producción (`https://live.fiscalapi.com`) en lugar de pruebas.                                       |
| `--api-base <url>` | Otra URL base, por ejemplo una API local. Gana sobre `--live` ([Ambientes](autenticacion.md#ambientes)). |
| `--json`           | Escribe solo JSON en stdout.                                                                             |
| `--verbose`        | Muestra en stderr el detalle HTTP de cada llamada a la API, sin las credenciales.                        |

## stdout y stderr

`fiscalapi` separa los datos de los mensajes, para que puedas redirigir o procesar los datos sin ruido:

| Va a stdout                                                            | Va a stderr                                                    |
| ---------------------------------------------------------------------- | -------------------------------------------------------------- |
| Las tablas y los detalles (`webhooks list`, `events retrieve`)         | Los errores (`Error:`) y sus detalles                          |
| La línea de cada entrega de `listen`                                   | La línea `Ready. Your signing secret is` de `listen`           |
| `Signing secret:` de `create` y `rotate-secret`                        | El aviso de guardar el secreto de firma                        |
| El secreto de firma de `listen --print-secret`                         | Los prompts y mensajes de `login` y `logout`                   |
| `Resend created:` de `events resend` y `Event triggered:` de `trigger` | La confirmación de `webhooks delete` y `Webhook <id> deleted.` |
| `--version` y `--help` (sin `--json`)                                  | El detalle HTTP de `--verbose` y el aviso de versión nueva     |

```bash
fiscalapi webhooks list > webhooks.txt        # la tabla al archivo, los errores en la terminal
fiscalapi events list 2> /dev/null | less     # sin mensajes en el paginador
```

## `--json`

Con `--json`, stdout trae solo JSON y todo lo demás va a stderr:

| Comando                                               | stdout con `--json`                                                                                  |
| ----------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `webhooks`, `events list`, `events resend`, `trigger` | El `data` de la respuesta de la API, sin cambios.                                                    |
| `events retrieve`                                     | El evento como lo devuelve la API (igual que sin `--json`). No sirve para verificar una firma.       |
| `listen`                                              | Un objeto JSON por entrega, uno por línea (JSON Lines). Ver [`listen`](listen.md#salida-con---json). |
| `listen --print-secret`                               | `{"secret":"whsec_<secreto>"}`                                                                       |
| `login`, `logout`                                     | Nada: sus mensajes van a stderr.                                                                     |
| `--help`, `--version`                                 | Nada: con `--json` se escriben en stderr.                                                            |

Un error nunca escribe en stdout, ni siquiera con `--json`: si el código de salida no es 0, stdout está vacío o
incompleto. Los errores siguen siendo texto en stderr.

```bash
if ! data=$(fiscalapi events list --type invoice.canceled --json); then
  echo "falló con código $?" >&2
fi
echo "$data" | jq '.totalCount'
```

## `--verbose`

`--verbose` escribe en stderr cada petición HTTP a la API y su respuesta, con el tiempo que tardó. La tabla del
resultado va después, en stdout:

```text
$ fiscalapi events list --page-size 1 --verbose
> GET https://test.fiscalapi.com/api/v4/events?pageSize=1
> User-Agent: fiscalapi-cli/4.0.469
< 200 OK (154 ms)
< Content-Type: application/json; charset=utf-8
```

Los headers de la respuesta varían según el servidor. Un fallo de red aparece así, una vez por intento:

```text
! GET https://test.fiscalapi.com/api/v4/events?pageSize=1 failed (ConnectionError) after 12 ms: <motivo>
```

`--verbose` nunca muestra, ni enmascarados:

- los headers de credenciales: `X-API-KEY`, `X-TENANT-KEY`, `Authorization`, `Proxy-Authorization`, `Cookie` y
  `Set-Cookie`;
- los cuerpos de las peticiones y respuestas, porque pueden traer secretos (como el secreto de firma de un webhook).

Así puedes pegar la salida en un ticket de soporte. El detalle de `--verbose` es de las llamadas a la API, no de los
reenvíos de `listen` a tu servidor.

## Códigos de salida

| Código | Significado                 | Causas típicas                                                                                                                                                                                                                                                                                                |
| ------ | --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 0      | Éxito                       | El comando terminó bien. También `logout` sin nada guardado y `webhooks delete` cuando no confirmas.                                                                                                                                                                                                          |
| 1      | Error de la API o de la red | 401 (credenciales), 403 (sin `Webhooks.Manage`), 404, 400 de validación, 5xx; la API no respondió; `listen` reemplazado por otra sesión (409); `trigger` en producción (403) o sin `listen` abierto (409); el almacén de credenciales está bloqueado; actualizar tu sesión de `listen` con `webhooks update`. |
| 2      | Error de uso                | Opción o argumento inválido; faltan credenciales; solo una de `FISCALAPI_API_KEY` y `FISCALAPI_TENANT_KEY`; no hay almacén de credenciales; `webhooks delete` sin `--yes` en un script.                                                                                                                       |
| 130    | Cancelado                   | Ctrl+C.                                                                                                                                                                                                                                                                                                       |

Un error de la API muestra el mensaje, el detalle, el código HTTP, el `traceIdentifier` (dáselo a soporte) y, cuando
la hay, una sugerencia. El mensaje y el detalle los envía la API:

```text
$ fiscalapi webhooks list
Error: Sorry, it's not me, it's you.
  Details: Invalid tenant key
  HTTP status: 401
  traceIdentifier (for support): <traceIdentifier>
  Hint: Run `fiscalapi login` or set FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY.
```

Un 400 de validación agrega, debajo de `Details`, una línea `- <regla>` por cada regla que falló. Una respuesta que no
es JSON (por ejemplo, un 502 o 503 en HTML de un proxy) se muestra así:

```text
Error: Unexpected response (503).
  HTTP status: 503
```

Las consultas (GET) se reintentan solas, hasta 3 intentos en total con una espera corta y creciente (la primera de
0.25 a 0.5 s), ante una conexión fallida o cortada, 408, 429 y 5xx. Se respeta `Retry-After`; si pide más de 10 s, no
se reintenta. Nunca se reintentan una API que no responde a tiempo, un error de DNS o de TLS, otro 4xx ni un POST, PUT
o DELETE.

Siguiente: [Solución de problemas](solucion-de-problemas.md).
