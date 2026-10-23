# `fiscalapi listen`

Recibe en tu equipo los eventos de webhook de tu cuenta y los reenvía a tu servidor local, sin exponerlo a internet.

## Uso

```text
fiscalapi listen [--forward-to <url>] [--events <types>] [--headers "Name:Value,Name:Value"] [--skip-verify] [--print-secret]
```

Requiere una API key con el permiso `Webhooks.Manage` ([Autenticación](autenticacion.md)).

## Opciones

| Opción                | Descripción                                                                                                                              |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `--forward-to <url>`  | URL absoluta `http` o `https` de tu servidor, por ejemplo `http://localhost:3000/webhooks`. Sin ella, `listen` solo muestra los eventos. |
| `--events <types>`    | Tipos separados por coma, por ejemplo `invoice.canceled,invoice.cancellation_rejected`. Por omisión, `*` (todos).                        |
| `--headers <headers>` | Headers extra en cada reenvío, como `"Name:Value,Name:Value"`. El valor puede llevar `:`.                                                |
| `--skip-verify`       | No verifica el certificado TLS de `--forward-to` (por ejemplo, uno autofirmado). Nunca afecta a la conexión con la API.                  |
| `--print-secret`      | Imprime solo el secreto de firma de una sesión nueva en stdout y termina.                                                                |

También acepta las opciones globales `--live`, `--api-base`, `--json` y `--verbose`
([Salida y códigos](salida-y-codigos.md)).

Estas combinaciones son errores de uso (salida 2):

| Caso                                                               | Error                                                                         |
| ------------------------------------------------------------------ | ----------------------------------------------------------------------------- |
| `--forward-to` que no es una URL absoluta                          | `Error: --forward-to must be an absolute http or https URL. Got 'localhost'.` |
| `--headers` o `--skip-verify` sin `--forward-to`                   | `Error: --skip-verify requires --forward-to.`                                 |
| `--headers` con un header firmado                                  | `Error: --headers cannot set webhook-id. FiscalAPI signs it.`                 |
| `--headers` sin la forma `Nombre:Valor`                            | `Error: --headers expects comma-separated Name:Value pairs. Got 'X-Env'.`     |
| `--print-secret` con `--forward-to`, `--headers` o `--skip-verify` | `Error: --print-secret cannot be used with --forward-to.`                     |

Cada error de uso termina con la línea ``Run `fiscalapi --help` to see commands and options.``

`--print-secret` abre una sesión de `listen`, igual que `listen`. Si ya corre un `fiscalapi listen` con la misma API
key, lo reemplaza y aquel termina con un [409](#conexión-y-sesiones). Úsalo antes de arrancar `listen`.

## Ejemplos

Reenvía los eventos a tu servidor local:

```text
$ fiscalapi listen --forward-to http://localhost:3000/webhooks
Ready. Your signing secret is whsec_<secreto> (listen session <id>).
2026-10-12 09:47:46  invoice.canceled [3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b] [200]
```

Cada línea trae la hora local, el tipo, el id del evento (el header `webhook-id`) y lo que respondió tu servidor. Para
disparar un evento de prueba, corre [`fiscalapi trigger`](trigger.md) en otra terminal.

```bash
# Solo muestra las cancelaciones, sin reenviarlas
fiscalapi listen --events invoice.canceled,invoice.cancellation_rejected

# Servidor con TLS autofirmado y un header de autorización propio
fiscalapi listen --forward-to https://localhost:5443/webhooks --skip-verify --headers "Authorization:Bearer token-local"

# Guarda el secreto de firma para configurar tu servidor
export FISCALAPI_WEBHOOK_SECRET=$(fiscalapi listen --print-secret)
```

## Cómo funciona

1. `listen` abre tu sesión de `listen`: un webhook sin URL, uno por persona, con su propio secreto de firma. El
   secreto de firma se conserva entre reinicios: configúralo una vez en tu servidor.
2. Cada evento de tu cuenta de los tipos de `--events` genera una entrega para tu sesión de `listen`.
3. `listen` reenvía cada entrega a `--forward-to` como un `POST` con el cuerpo exacto que firmó FiscalAPI
   (`Content-Type: application/json; charset=utf-8`), los headers firmados `webhook-id`, `webhook-timestamp` y
   `webhook-signature`, y los de `--headers`.
4. Le informa a la API el resultado, así la entrega queda en el historial como la de un webhook con URL.

Se reenvían hasta 16 entregas a la vez; las de un mismo evento conservan su orden. Las redirecciones no se siguen.

No hay reintentos. Si tu servidor falla, la entrega queda con ese resultado; repítela con
[`fiscalapi events resend`](webhooks-y-eventos.md#reenviar-un-evento).

## Resultado de cada reenvío

| Final de la línea           | Significado                                                                                    |
| --------------------------- | ---------------------------------------------------------------------------------------------- |
| `[200]`                     | Tu servidor respondió ese código. Cualquier código de 100 a 599 se muestra tal cual.           |
| `[error: Timeout]`          | Tu servidor no respondió en 15 s.                                                              |
| `[error: ConnectionFailed]` | No se pudo conectar: nada escucha en ese puerto, falla el DNS o se rechazó la conexión.        |
| `[error: TlsFailed]`        | Falló el TLS hacia `--forward-to`, por ejemplo un certificado autofirmado sin `--skip-verify`. |
| `[error: InvalidResponse]`  | La respuesta no es HTTP válido o se cortó.                                                     |
| (nada)                      | Sin `--forward-to`: el evento solo se mostró y cuenta como entregado.                          |

## Verificar la firma

Cada reenvío lleva la firma de FiscalAPI con el secreto de firma de tu sesión de `listen` (`whsec_`), según
[Standard Webhooks](https://www.standardwebhooks.com/). Verifícala como la de un webhook con URL, con el cuerpo crudo
tal como llegó: si lo deserializas y lo vuelves a serializar, cambian sus bytes y la firma no coincide.

Los SDK de FiscalAPI traen la verificación:

| SDK                   | Función                                                        |
| --------------------- | -------------------------------------------------------------- |
| Node.js (`fiscalapi`) | `Webhook.verify(headers, body, secret)`                        |
| Python (`fiscalapi`)  | `fiscalapi.webhooks.verify(headers, body, secret)`             |
| PHP                   | `Fiscalapi\Webhooks\Webhook::verify(...)`                      |
| Java                  | `com.fiscalapi.webhooks.Webhook.verify(headers, body, secret)` |

La forma del evento, la verificación a mano y el resto de los SDK están en la
[guía de webhooks de FiscalAPI](https://docs.fiscalapi.com/webhooks-info).

## Conexión y sesiones

`listen` corre hasta que lo detienes. Si la primera apertura de la sesión de `listen` falla (por ejemplo, un
`--api-base` equivocado o una API key sin permiso), termina de inmediato con el error. Después:

| Situación                            | Qué hace `listen`                                                                                                     |
| ------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| Red caída, 408, 429 o 5xx            | Reintenta con espera creciente y aleatoria (la primera de 0.5 a 1 s, como máximo 30 s) y lo avisa en stderr.          |
| 410, sesión caducada                 | La API cerró la sesión por falta de conexión (un corte largo, el equipo suspendido). `listen` abre una nueva y sigue. |
| 409, sesión reemplazada              | Otro `listen` o un `--print-secret` con la misma API key abrió sesión. Solo puede haber una: esta termina con 1.      |
| Otro error de la API (401, 403, 404) | Termina con 1 y el error de la API.                                                                                   |
| Ctrl+C                               | Informa durante 2 s los resultados que ya tenía y sale con 130.                                                       |

Los avisos de reconexión van a stderr:

```text
Lost connection to FiscalAPI (Could not connect to https://test.fiscalapi.com/: <motivo>). Retrying in 0.8 s.
Reconnected to FiscalAPI.
The listen session expired. Opening a new one.
```

Los eventos que ocurren sin una sesión de `listen` abierta no llegan después. Búscalos con `fiscalapi events list` y
reenvíalos con `fiscalapi events resend <eventId>`.

Una sesión reemplazada termina así (el texto de `Details` lo envía la API):

```text
Error: Sorry, it's not me, it's you.
  Details: Otra sesión de fiscalapi listen reemplazó a esta.
  HTTP status: 409
  traceIdentifier (for support): <traceIdentifier>
```

Otros avisos en stderr:

| Aviso                                                                                            | Qué pasó                                                                                                                     |
| ------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| `Delivery <deliveryId> (<eventId>) expired before forwarding. The API closed it unacknowledged.` | La entrega esperó su turno más de 2 minutos y la API ya la cerró: no se reenvía.                                             |
| `Could not acknowledge delivery <deliveryId>: <motivo>`                                          | La API no registró el resultado de la entrega. Si la entrega ya estaba cerrada (409), el aviso solo aparece con `--verbose`. |
| `Could not reopen the listen session (<motivo>). Retrying in <n> s.`                             | Al reabrir la sesión, otra apertura con la misma API key estaba en curso. Se reintenta con espera creciente.                 |

## Salida con `--json`

Con `--json`, stdout lleva un objeto JSON por entrega, uno por línea (JSON Lines). Los avisos, los errores y la línea
`Ready. Your signing secret is` siguen en stderr como texto.

```json
{"time":"2026-10-12T09:47:46.1234567-06:00","deliveryId":"a7b8c9d0-e1f2-4a3b-8c4d-5e6f7a8b9c0d","eventId":"3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b","type":"invoice.canceled","statusCode":200,"latencyMs":12,"event":{"id":"3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b","type":"invoice.canceled","createdAt":"2026-10-12T15:47:46Z","personId":null,"data":{"relatedObject":{"id":"c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f","type":"invoice","url":"/api/v4/invoices/c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f"},"status":"X","previousStatus":"C"}}}
```

| Campo        | Contenido                                                                                                       |
| ------------ | --------------------------------------------------------------------------------------------------------------- |
| `time`       | Cuándo terminó el reenvío, en hora local con su desfase.                                                        |
| `deliveryId` | Id de la entrega.                                                                                               |
| `eventId`    | El header `webhook-id`, o `?` si no vino.                                                                       |
| `type`       | El tipo del evento, o `?` si el cuerpo no es un evento.                                                         |
| `statusCode` | Lo que respondió tu servidor. Solo con `--forward-to` y si respondió.                                           |
| `latencyMs`  | Lo que tardó el reenvío. Solo con `--forward-to`.                                                               |
| `error`      | `Timeout`, `ConnectionFailed`, `TlsFailed` o `InvalidResponse`. Solo con `--forward-to` y si no hubo respuesta. |
| `event`      | El evento tal como llegó. Se omite si el cuerpo no es JSON.                                                     |

Con `--print-secret --json`, la única línea es `{"secret":"whsec_<secreto>"}`.

```bash
# El tipo y el resultado de cada reenvío
fiscalapi listen --forward-to http://localhost:3000/webhooks --json | jq -r '"\(.type) \(.statusCode // .error)"'
```

## Docker y terminales sin TTY

Dentro de un contenedor, `http://localhost` es el propio contenedor. Para llegar a tu servidor en el equipo anfitrión
usa su nombre de red (en Docker Desktop, `http://host.docker.internal:3000/webhooks`) y pasa las credenciales con
`-e FISCALAPI_API_KEY -e FISCALAPI_TENANT_KEY`.

`listen` funciona en cualquier terminal. Solo `login` necesita una terminal real para ocultar lo que escribes
([Autenticación](autenticacion.md#sin-terminal-stdin)).

Siguiente: [Webhooks y eventos](webhooks-y-eventos.md).
