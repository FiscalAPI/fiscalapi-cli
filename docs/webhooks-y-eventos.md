# Webhooks y eventos

`fiscalapi webhooks` administra los webhooks de tu cuenta (`/api/v4/webhooks`) y `fiscalapi events` consulta y reenvía
los eventos de los últimos 30 días (`/api/v4/events`). Requieren una API key cuya persona tenga el permiso
`Webhooks.Manage`.

Los ejemplos de salida de esta página salen del CLI real con los datos de sus pruebas automatizadas.

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
escucha-1                             cliListener  enabled  -                             *                                                -
Página 1 de 3 (5 en total). Siguiente: --page 2
```

`TIPO` es `url` para un webhook con URL y `cliListener` para tu [escucha de `fiscalapi listen`](listen.md), que no tiene
URL. El pie dice qué opción pedir para la página siguiente.

### Paginación

`webhooks list` y `events list` aceptan:

| Opción | Uso |
|---|---|
| `--page <número>` | Número de página, desde 1 (por omisión 1). |
| `--page-size <tamaño>` | Elementos por página, de 1 a 50 (por omisión 10). |

### Ver un webhook

```text
$ fiscalapi webhooks retrieve 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
ID           6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Tipo         url
Estado       enabled
URL          https://example.com/webhooks
Descripción  Producción
Eventos      invoice.canceled, invoice.cancellation_requested
Personas     persona-1, persona-2
Creado       2026-10-13T09:30:00.000
```

Solo se muestran los campos con valor. Según el caso aparecen también `Motivo` y `Deshabilitado` (un webhook
deshabilitado), `Fallando desde`, `Persona de la escucha` y `Sesión vence` (una escucha de CLI) y `Actualizado`.

### Crear

| Opción | Uso |
|---|---|
| `--url <url>` | **Obligatoria.** URL `https` que recibe las entregas. |
| `--events <tipos>` | **Obligatoria.** Tipos separados por coma, o `*` para todos. |
| `--description <texto>` | Descripción libre, hasta 256 caracteres. |
| `--person <id>` | Solo los eventos de esta persona. Se repite (`--person a --person b`) o va separado por coma. |
| `--all-persons` | Los eventos de todas las personas. Es lo que pasa si no indicas `--person`. |

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

**El secreto se muestra una sola vez.** `list` y `retrieve` nunca lo muestran: cópialo a la configuración segura de tu
receptor en ese momento. Si lo pierdes, genera otro con `rotate-secret`. La línea `Secreto de firma: …` va a stdout y el
aviso, a stderr.

En bash y zsh escribe `*` entre comillas (`'*'`) para que el shell no lo expanda como comodín de archivos.

Los tipos de evento son `invoice.cancellation_requested`, `invoice.canceled`, `invoice.cancellation_rejected`,
`download_request.completed`, `download_request.abandoned` y `download_rule.suspended`. La API valida la URL y los
tipos; si algo no es válido, responde 400 con el detalle de cada regla.

### `--person` y `--all-persons`

Un webhook recibe los eventos de todas las personas de tu cuenta o solo los de las personas que indiques:

```bash
fiscalapi webhooks create --url https://example.com/webhooks --events invoice.canceled --person persona-1,persona-2
fiscalapi webhooks update 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --all-persons   # quita el filtro
```

`--person` vacío es un error, para que una variable vacía (`--person "$PERSONAS"`) no amplíe el webhook a todas las
personas sin querer:

```text
Error: --person necesita al menos un id de persona; para recibir los eventos de todas usa --all-persons.
```

`--all-persons` y `--person` no se combinan.

### Actualizar

`update` lee el webhook, cambia solo lo que indiques y lo guarda. Acepta las opciones de `create` (todas opcionales)
y además:

| Opción | Uso |
|---|---|
| `--enabled` | Habilita el webhook; limpia el motivo y la fecha de la deshabilitación. |
| `--disabled` | Deshabilita el webhook y cierra sus entregas pendientes. |

```bash
fiscalapi webhooks update 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --events invoice.canceled,invoice.cancellation_rejected
fiscalapi webhooks update 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --disabled
```

Imprime el webhook ya actualizado. Sin ninguna opción es un error de uso:

```text
Error: Indica qué cambiar: --url, --description, --events, --person, --all-persons, --enabled o --disabled.
```

Tu escucha de CLI no se edita: `update` sobre ella sale con 1 y
`Error: Una escucha de CLI no se edita ni se prueba; usa fiscalapi listen y fiscalapi trigger.`

### Borrar

En una terminal, `delete` pregunta antes de borrar. Solo `s`, `si`, `sí`, `y` o `yes` confirman:

```text
$ fiscalapi webhooks delete 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
¿Borrar el webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d? Sus entregas pendientes se cerrarán. [s/N] s
Webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d borrado.
```

Si respondes otra cosa: `No se borró el webhook <id>.` (sale con 0). En un script o con `--json` no hay a quién
preguntar: usa `--yes`.

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
una lista) y quita el anterior al terminar. `rotate-secret` también funciona sobre el id de tu escucha de CLI.

## `fiscalapi events`

| Comando | Ruta | Qué hace |
|---|---|---|
| `events list` | `GET /api/v4/events` | Lista los eventos de los últimos 30 días, del más nuevo al más viejo. |
| `events retrieve <id>` | `GET /api/v4/events/{id}` | Imprime el evento tal como se entregó. |
| `events resend <eventId>` | `POST /api/v4/events/{id}/resend` | Reenvía un evento a un webhook o a tu escucha de `fiscalapi listen`. |

### Listar eventos

| Opción | Uso |
|---|---|
| `--type <tipo>` | Solo los eventos de este tipo (p. ej. `invoice.canceled` o `webhook.test`). |
| `--related-object-id <id>` | Solo los eventos de este recurso (`data.relatedObject.id`), p. ej. el id de una factura. |
| `--from <fecha>` | Desde este instante, incluido. |
| `--to <fecha>` | Hasta este instante, incluido. Por omisión, ahora. |
| `--page`, `--page-size` | [Paginación](#paginación). |

```text
$ fiscalapi events list
ID                                    TIPO              CREADO (UTC)          RECURSO    PERSONA
0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f  invoice.canceled  2026-10-12T15:47:46Z  factura-1  persona-1
evt-2                                 webhook.test      2026-10-12T15:40:00Z  we-1       -
Página 1 de 1 (2 en total).
```

### Fechas

`--from` y `--to` aceptan ISO 8601:

| Escribes | Significa |
|---|---|
| `2026-10-01` | Con `--from`, el inicio del día en UTC. Con `--to`, **el día completo**, hasta las 23:59:59.9999999 UTC. |
| `2026-10-01T08:00` o `2026-10-01T08:00:00` | Ese instante en UTC. |
| `2026-10-01T08:00:00-06:00` | Ese instante con su zona horaria. |

Sin zona, la hora se toma como UTC. Otro formato es un error de uso:

```text
$ fiscalapi events list --from ayer
Error: --from debe ser una fecha ISO 8601 (p. ej. 2026-10-01 o 2026-10-01T08:00:00-06:00); se recibió 'ayer'.
```

```bash
# Las cancelaciones de una factura en octubre
fiscalapi events list --type invoice.canceled --related-object-id factura-1 --from 2026-10-01 --to 2026-10-31
```

### Ver un evento

`events retrieve` imprime el evento exactamente como lo entrega la API, con o sin `--json`:

```text
$ fiscalapi events retrieve 0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f
{"id":"0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f","type":"invoice.canceled","createdAt":"2026-10-12T15:47:46Z","personId":"persona-1","data":{"relatedObject":{"id":"factura-1","type":"invoice","url":"/api/v4/invoices/factura-1"},"status":"X","previousStatus":"C"}}
```

El `id` del evento es el mismo del header `webhook-id` de sus entregas.

### Reenviar un evento

```text
$ fiscalapi events resend 0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f --webhook we-1
Reenvío creado: entrega entrega-1 al webhook we-1.
```

- Con `--webhook <id>`, el evento se reenvía a ese webhook (cuerpo `{"webhookId":"<id>"}`).
- Sin `--webhook`, va a **tu escucha de CLI**, que tiene que tener un `fiscalapi listen` abierto. Es la forma de recibir
  en local un evento que ocurrió mientras `listen` no corría.

El reenvío crea una entrega nueva con el mismo evento: mismo `webhook-id` y mismo cuerpo. Tu receptor debe descartar
los ids que ya procesó.

## `--json`

Con `--json`, cada comando imprime en stdout el `data` de la respuesta de la API, sin cambios, para procesarlo con otra
herramienta:

```bash
# Los ids de los webhooks habilitados
fiscalapi webhooks list --page-size 50 --json | jq -r '.items[] | select(.status == "enabled") | .id'

# El secreto de un webhook nuevo, directo a una variable
secret=$(fiscalapi webhooks create --url https://example.com/webhooks --events '*' --json | jq -r .secret)
```

Una página trae `items`, `pageNumber`, `totalPages`, `totalCount`, `hasPreviousPage` y `hasNextPage`. Los mensajes
para la persona (el aviso del secreto, `Webhook … borrado.`) y los errores siguen en stderr como texto. Ver
[Salida y códigos](salida-y-codigos.md).

Siguiente: [`fiscalapi trigger`](trigger.md).
