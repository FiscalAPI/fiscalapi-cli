# Salida y códigos de salida

## stdout y stderr

`fiscalapi` separa los datos de los mensajes, para que puedas redirigir o procesar los datos sin ruido:

| Va a stdout | Va a stderr |
|---|---|
| Las tablas y los detalles (`webhooks list`, `events retrieve`…) | Los errores (`Error: …`) y sus detalles |
| La línea de cada entrega de `listen` | `Listo. Tu secreto de firma es …` de `listen` |
| `Secreto de firma: whsec_…` de `create` y `rotate-secret` | El aviso de guardar el secreto |
| El secreto de `listen --print-secret` | Los prompts y mensajes de `login` y `logout` |
| `Reenvío creado: …` de `events resend` y `Evento disparado: …` de `trigger` | La confirmación de `webhooks delete` y `Webhook … borrado.` |
| `--version` y `--help` (sin `--json`) | El detalle HTTP de `--verbose` y el aviso de versión nueva |

```bash
fiscalapi webhooks list > webhooks.txt        # la tabla al archivo, los errores en la terminal
fiscalapi events list 2> /dev/null | less     # sin mensajes en el paginador
```

## `--json`

Con `--json`, **stdout trae solo JSON** y todo lo demás va a stderr:

| Comando | stdout con `--json` |
|---|---|
| `webhooks …`, `events list`, `events resend`, `trigger` | El `data` de la respuesta de la API, sin cambios. |
| `events retrieve` | El evento tal como se entregó (igual que sin `--json`). |
| `listen` | Un objeto JSON por entrega, uno por línea (JSON Lines). Ver [listen](listen.md#salida-con---json). |
| `listen --print-secret` | `{"secret":"whsec_…"}` |
| `login`, `logout` | Nada: sus mensajes van a stderr. |
| `--help`, `--version` | Nada: con `--json` se escriben en stderr. |

Un error nunca escribe en stdout, ni siquiera con `--json`: si el código de salida no es 0, stdout está vacío o
incompleto. Los errores siguen siendo texto en stderr.

```bash
if ! data=$(fiscalapi events list --type invoice.canceled --json); then
  echo "falló con código $?" >&2
fi
echo "$data" | jq '.totalCount'
```

## `--verbose`

`--verbose` escribe en stderr cada petición HTTP a la API y su respuesta, con el tiempo que tardó. Este ejemplo es
contra una API falsa local; la tabla del resultado va después, en stdout:

```text
$ fiscalapi events list --page-size 1 --verbose --api-base http://localhost:5099
> GET http://localhost:5099/api/v4/events?pageSize=1
> User-Agent: fiscalapi-cli/0.1.0
< 200 OK (2037 ms)
< Server: BaseHTTP/0.6, Python/3.12.10
< Date: Tue, 13 Oct 2026 23:54:21 GMT
< Content-Type: application/json; charset=utf-8
< Content-Length: 679
```

Un fallo de red aparece como `! GET … falló (ConnectionError) tras 12 ms: …`.

`--verbose` **nunca** muestra, ni enmascarados:

- los headers de credenciales: `X-API-KEY`, `X-TENANT-KEY`, `Authorization`, `Proxy-Authorization`, `Cookie` y
  `Set-Cookie`;
- los cuerpos de las peticiones y respuestas, porque pueden traer secretos (como el `whsec_` de un webhook).

Así puedes pegar la salida en un ticket de soporte. El detalle de `--verbose` es de las llamadas a la API, no de los
reenvíos de `listen` a tu servidor.

## Códigos de salida

| Código | Significado | Causas típicas |
|---|---|---|
| 0 | Éxito | El comando terminó bien. También `logout` sin nada guardado y `webhooks delete` cuando respondes "no". |
| 1 | Error de la API o de la red | 401 (credenciales), 403 (sin `Webhooks.Manage`), 404, 400 de validación, 5xx; la API no respondió; `listen` reemplazado por otra sesión (409); `trigger` en producción (403) o sin `listen` abierto (409); el almacén de credenciales está bloqueado; editar tu escucha de CLI. |
| 2 | Error de uso | Opción o argumento inválido; falta de credenciales; solo una de `FISCALAPI_API_KEY`/`FISCALAPI_TENANT_KEY`; no hay almacén de credenciales; `delete` sin `--yes` en un script. |
| 130 | Cancelado | Ctrl+C. |

Un error de la API muestra el mensaje, el detalle, el código HTTP, el `traceIdentifier` (dáselo a soporte) y, cuando
la hay, una sugerencia:

```text
$ fiscalapi webhooks list
Error: Sorry, it's not me, it's you.
  Detalle: Invalid tenant key
  Código HTTP: 401
  traceIdentifier (para soporte): 4C2E9A7B1D3F5E608B9A0C1D2E3F4A5B
  Sugerencia: Ejecuta `fiscalapi login` o revisa las variables de entorno FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.
```

Un 400 de validación agrega, debajo del detalle, una línea `- <regla>` por cada regla que falló. Una respuesta que no
es JSON (por ejemplo, un 502 o 503 en HTML de un proxy) se muestra como `Error: Respuesta inesperada (503).`

Los GET se reintentan solos, hasta 3 intentos en total con espera creciente (de 0.5 s hasta 10 s), ante una conexión
fallida o cortada, 408, 429 y 5xx. Se respeta `Retry-After`; si pide más de 10 s, no se reintenta. Nunca se reintentan
una API que no responde a tiempo, un error de DNS o de TLS, otro 4xx ni un POST, PUT o DELETE.

Siguiente: [Solución de problemas](solucion-de-problemas.md).
