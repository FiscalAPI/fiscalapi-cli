# Webhooks y eventos

`fiscalapi webhooks` administra los webhooks de tu cuenta y `fiscalapi events` consulta y reenvía los eventos de los
últimos 30 días. Requieren una API key con el permiso `Webhooks.Manage` ([Autenticación](autenticacion.md)).

## `fiscalapi webhooks`

| Comando | Ruta | Qué hace |
|---|---|---|
| `webhooks list` | `GET /api/v4/webhooks` | Lista los webhooks, sin su secreto. |
| `webhooks retrieve <id>` | `GET /api/v4/webhooks/{id}` | Muestra un webhook, sin su secreto. |
| `webhooks create` | `POST /api/v4/webhooks` | Da de alta un webhook e imprime su secreto **una sola vez**. |
| `webhooks update <id>` | `GET` y `PUT /api/v4/webhooks/{id}` | Cambia URL, descripción, eventos, personas o estado. |
| `webhooks delete <id>` | `DELETE /api/v4/webhooks/{id}` | Da de baja un webhook y cierra sus entregas pendientes; su historial se conserva. |
| `webhooks rotate-secret <id>` | `POST /api/v4/webhooks/{id}/rotate-secret` | Genera un secreto nuevo y lo imprime una sola vez. |

### Listar webhooks

```text
$ fiscalapi webhooks list
ID                                    TIPO         ESTADO   URL                           EVENTOS                                          DESCRIPCIÓN
6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d  url          enabled  https://example.com/webhooks  invoice.canceled,invoice.cancellation_requested  Producción
9b2e7c4a-1d3f-4e6b-8a5c-0f1e2d3c4b5a  cliListener  enabled  -                             *                                                -
Página 1 de 3 (5 en total). Siguiente: --page 2
```

`TIPO` es `url` para un webhook con URL y `cliListener` para tu [escucha de `fiscalapi listen`](listen.md), que no
tiene URL. El pie indica la opción para pedir la página siguiente.

### Paginación

`webhooks list` y `events list` aceptan:

| Opción | Descripción |
|---|---|
| `--page <número>` | Número de página, desde 1. Por omisión, 1. |
| `--page-size <tamaño>` | Elementos por página, de 1 a 50. Por omisión, 10. |

### Ver un webhook

```text
$ fiscalapi webhooks retrieve 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
ID           6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Tipo         url
Estado       enabled
URL          https://example.com/webhooks
Descripción  Producción
Eventos      invoice.canceled, invoice.cancellation_requested
Personas     2e7d4c1b-9a8f-4e6d-b5c4-3a2b1c0d9e8f, 7c3b2a1d-0e9f-4d8c-a7b6-5f4e3d2c1b0a
Creado       2026-10-13T09:30:00.000
```

Solo aparecen los campos con valor. Según el caso también verás `Motivo` y `Deshabilitado` (webhook deshabilitado),
`Fallando desde`, `Persona de la escucha` y `Sesión vence` (escucha de CLI) y `Actualizado`.

### Crear un webhook

| Opción | Descripción |
|---|---|
| `--url <url>` | **Obligatoria.** URL `https` que recibe las entregas. |
| `--events <tipos>` | **Obligatoria.** Tipos separados por coma, o `*` para todos. |
| `--description <texto>` | Descripción libre, hasta 256 caracteres. |
| `--person <id>` | Solo los eventos de esta persona, hasta 100. Se repite (`--person a --person b`) o va separado por coma. |
| `--all-persons` | Los eventos de todas las personas. Es el comportamiento sin `--person`. |

```text
$ fiscalapi webhooks create --url https://example.com/webhooks --events '*'
ID        6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Tipo      url
Estado    enabled
URL       https://example.com/webhooks
Eventos   *
Personas  todas
Creado    2026-10-13T09:30:00.000

Secreto de firma: whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw
Guarda este secreto de firma. Lo usarás para verificar que los eventos vienen de FiscalAPI.
```

**El secreto se muestra una sola vez.** `list` y `retrieve` nunca lo muestran: guárdalo en ese momento en la
configuración segura de tu receptor. Si lo pierdes, genera otro con `rotate-secret`. La línea `Secreto de firma: …` va
a stdout y el aviso, a stderr.

En bash y zsh, escribe `'*'` entre comillas para que el shell no lo expanda.

Los tipos de evento son `invoice.cancellation_requested`, `invoice.canceled`, `invoice.cancellation_rejected`,
`download_request.completed`, `download_request.abandoned` y `download_rule.suspended`. La API valida la URL y los
tipos; si algo no es válido, responde 400 con una línea por cada regla que falló.

### Filtrar por persona

Un webhook recibe los eventos de todas las personas de tu cuenta o solo los de las que indiques:

```bash
fiscalapi webhooks create --url https://example.com/webhooks --events invoice.canceled --person 2e7d4c1b-9a8f-4e6d-b5c4-3a2b1c0d9e8f,7c3b2a1d-0e9f-4d8c-a7b6-5f4e3d2c1b0a
fiscalapi webhooks update 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --all-persons   # quita el filtro
```

`--person` vacío es un error, para que una variable vacía (`--person "$PERSONAS"`) no amplíe el webhook a todas las
personas sin querer:

```text
Error: --person necesita al menos un id de persona; para recibir los eventos de todas usa --all-persons.
```

`--person` y `--all-persons` no se combinan.

### Actualizar un webhook

`update` lee el webhook, cambia solo lo que indicas y lo guarda. Acepta las opciones de `create`, todas opcionales, y
además:

| Opción | Descripción |
|---|---|
| `--enabled` | Habilita el webhook y limpia el motivo y la fecha de la deshabilitación. |
| `--disabled` | Deshabilita el webhook y cierra sus entregas pendientes. |

```bash
fiscalapi webhooks update 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --events invoice.canceled,invoice.cancellation_rejected
fiscalapi webhooks update 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --disabled
```

Imprime el webhook actualizado. Estos casos fallan:

| Caso | Salida | Error |
|---|---|---|
| Sin ninguna opción | 2 | `Error: Indica qué cambiar: --url, --description, --events, --person, --all-persons, --enabled o --disabled.` |
| `--enabled` con `--disabled` | 2 | `Error: --enabled y --disabled no se combinan.` |
| El id es tu escucha de CLI | 1 | `Error: Una escucha de CLI no se edita ni se prueba; usa fiscalapi listen y fiscalapi trigger.` |

### Borrar un webhook

En una terminal, `delete` pide confirmación. Solo `s`, `si`, `sí`, `y` o `yes` confirman:

```text
$ fiscalapi webhooks delete 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
¿Borrar el webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d? Sus entregas pendientes se cerrarán. [s/N] s
Webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d borrado.
```

Cualquier otra respuesta imprime `No se borró el webhook <id>.` y sale con 0. En un script (stdin redirigido) o con
`--json` no hay a quién preguntar: agrega `--yes`, o sale con 2.

```text
$ fiscalapi webhooks delete 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --json
Error: Para borrar sin confirmación interactiva usa --yes.
```

### Rotar el secreto

```text
$ fiscalapi webhooks rotate-secret 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
ID        6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
…
Secreto de firma: whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw
Guarda este secreto de firma. Lo usarás para verificar que los eventos vienen de FiscalAPI.
El secreto anterior seguirá firmando durante 24 horas para que actualices tu receptor sin perder eventos.
```

Durante esas 24 horas cada entrega lleva las dos firmas: configura tu receptor con los dos secretos (los SDK aceptan
una lista) y quita el anterior al terminar. Firman como mucho dos secretos: si vuelves a rotar dentro de esas 24 horas,
el anterior sigue siendo el original y el intermedio deja de firmar.

`rotate-secret` también funciona con el id de tu escucha de CLI.

## `fiscalapi events`

| Comando | Ruta | Qué hace |
|---|---|---|
| `events list` | `GET /api/v4/events` | Lista los eventos de los últimos 30 días, del más nuevo al más viejo. |
| `events retrieve <id>` | `GET /api/v4/events/{id}` | Imprime el evento tal como se entregó. |
| `events resend <eventId>` | `POST /api/v4/events/{id}/resend` | Reenvía un evento a un webhook o a tu escucha de `fiscalapi listen`. |

### Listar eventos

| Opción | Descripción |
|---|---|
| `--type <tipo>` | Solo los eventos de este tipo, p. ej. `invoice.canceled` o `webhook.test`. |
| `--related-object-id <id>` | Solo los eventos de este recurso (`data.relatedObject.id`), p. ej. el id de una factura. |
| `--from <fecha>` | Desde este instante, incluido. |
| `--to <fecha>` | Hasta este instante, incluido. Por omisión, ahora. |
| `--page`, `--page-size` | Ver [Paginación](#paginación). |

```text
$ fiscalapi events list
ID                                    TIPO              CREADO (UTC)          RECURSO                               PERSONA
0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f  invoice.canceled  2026-10-12T15:47:46Z  c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f  2e7d4c1b-9a8f-4e6d-b5c4-3a2b1c0d9e8f
5e8a7b6c-3d2f-4a1e-9b0c-8d7e6f5a4b3c  webhook.test      2026-10-12T15:40:00Z  6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d  -
Página 1 de 1 (2 en total).
```

```bash
# Las cancelaciones de una factura en octubre
fiscalapi events list --type invoice.canceled --related-object-id c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f --from 2026-10-01 --to 2026-10-31
```

### Fechas

`--from` y `--to` aceptan ISO 8601:

| Escribes | Significa |
|---|---|
| `2026-10-01` | Con `--from`, el inicio del día en UTC. Con `--to`, **el día completo**, hasta las 23:59:59.9999999 UTC. |
| `2026-10-01T08:00` o `2026-10-01T08:00:00` | Ese instante en UTC. |
| `2026-10-01T08:00:00-06:00` | Ese instante en esa zona horaria. |

Sin zona, la hora se toma como UTC. Otro formato es un error de uso (salida 2):

```text
$ fiscalapi events list --from ayer
Error: --from debe ser una fecha ISO 8601 (p. ej. 2026-10-01 o 2026-10-01T08:00:00-06:00); se recibió 'ayer'.
```

### Ver un evento

`events retrieve` imprime el evento exactamente como lo entrega la API, con o sin `--json`. Su `id` es el del header
`webhook-id` de sus entregas.

```text
$ fiscalapi events retrieve 0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f
{"id":"0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f","type":"invoice.canceled","createdAt":"2026-10-12T15:47:46Z","personId":"2e7d4c1b-9a8f-4e6d-b5c4-3a2b1c0d9e8f","data":{"relatedObject":{"id":"c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f","type":"invoice","url":"/api/v4/invoices/c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f"},"status":"X","previousStatus":"C"}}
```

### Reenviar un evento

```text
$ fiscalapi events resend 0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f --webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Reenvío creado: entrega d1e2f3a4-b5c6-4d7e-8f9a-0b1c2d3e4f5a al webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d.
```

| Opción | Descripción |
|---|---|
| `--webhook <id>` | Webhook que recibe el reenvío. Por omisión, tu escucha de CLI, que debe tener un `fiscalapi listen` abierto. |

Sin `--webhook` recibes en local un evento que ocurrió mientras `listen` no corría. Si no hay un `listen` abierto, la
API responde 409 (`La escucha de CLI no está conectada; ejecuta fiscalapi listen.`).

El reenvío crea una entrega nueva del mismo evento, con el mismo `webhook-id` y el mismo cuerpo. Tu receptor debe
descartar los ids que ya procesó.

## `--json`

Con `--json`, cada comando imprime en stdout el `data` de la respuesta de la API, sin cambios:

```bash
# Los ids de los webhooks habilitados
fiscalapi webhooks list --page-size 50 --json | jq -r '.items[] | select(.status == "enabled") | .id'

# El secreto de un webhook nuevo, directo a una variable
secret=$(fiscalapi webhooks create --url https://example.com/webhooks --events '*' --json | jq -r .secret)
```

Una página trae `items`, `pageNumber`, `totalPages`, `totalCount`, `hasPreviousPage` y `hasNextPage`. El aviso de
guardar el secreto y los errores siguen en stderr como texto. Más en [Salida y códigos](salida-y-codigos.md).

Siguiente: [`fiscalapi trigger`](trigger.md).
