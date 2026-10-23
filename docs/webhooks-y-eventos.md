# Webhooks y eventos

`fiscalapi webhooks` administra los webhooks de tu cuenta y `fiscalapi events` consulta y reenvía los eventos de los
últimos 30 días. Requieren una API key con el permiso `Webhooks.Manage` ([Autenticación](autenticacion.md)).

## `fiscalapi webhooks`

| Comando                       | Qué hace                                                                       |
| ----------------------------- | ------------------------------------------------------------------------------ |
| `webhooks list`               | Lista los webhooks, sin su secreto de firma.                                   |
| `webhooks retrieve <id>`      | Muestra un webhook, sin su secreto de firma.                                   |
| `webhooks create`             | Crea un webhook e imprime su secreto de firma una sola vez.                    |
| `webhooks update <id>`        | Cambia la URL, la descripción, los eventos, las personas o el estado.          |
| `webhooks delete <id>`        | Elimina un webhook y cierra sus entregas pendientes; su historial se conserva. |
| `webhooks rotate-secret <id>` | Genera un secreto de firma nuevo y lo imprime una sola vez.                    |

### Listar webhooks

```text
$ fiscalapi webhooks list --page-size 2
ID                                    KIND         STATUS   URL                           EVENTS                                           DESCRIPTION
6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d  url          enabled  https://example.com/webhooks  invoice.canceled,invoice.cancellation_requested  Producción
9b2e7c4a-1d3f-4e6b-8a5c-0f1e2d3c4b5a  cliListener  enabled  -                             *                                                -
Page 1 of 3 (5 total). Next: --page 2
```

`KIND` es `url` para un webhook con URL y `cliListener` para tu [sesión de `listen`](listen.md), que no tiene URL. El
pie indica la opción para pedir la página siguiente. Sin resultados, la tabla se reemplaza por `No data.`

### Paginación

`webhooks list` y `events list` aceptan:

| Opción               | Descripción                                       |
| -------------------- | ------------------------------------------------- |
| `--page <number>`    | Número de página, desde 1. Por omisión, 1.        |
| `--page-size <size>` | Elementos por página, de 1 a 50. Por omisión, 10. |

### Ver un webhook

```text
$ fiscalapi webhooks retrieve 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
ID           6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Kind         url
Status       enabled
URL          https://example.com/webhooks
Description  Producción
Events       invoice.canceled, invoice.cancellation_requested
Persons      2e7d4c1b-9a8f-4e6d-b5c4-3a2b1c0d9e8f, 7c3b2a1d-0e9f-4d8c-a7b6-5f4e3d2c1b0a
Created      2026-10-13T09:30:00.000
Updated      2026-10-13T09:30:00.000
```

Solo aparecen los campos con valor. Según el caso también verás `Disabled reason` y `Disabled at` (webhook
deshabilitado), `Failing since` (entregas que fallan), y `Listen session person` y `Session expires` (sesión de
`listen`).

### Crear un webhook

| Opción                 | Descripción                                                                                   |
| ---------------------- | --------------------------------------------------------------------------------------------- |
| `--url <url>`          | Obligatoria. URL `https` que recibe las entregas.                                             |
| `--events <types>`     | Obligatoria. Tipos separados por coma, o `*` para todos.                                      |
| `--description <text>` | Descripción libre, hasta 256 caracteres.                                                      |
| `--person <id>`        | Solo los eventos de esta persona. Se repite (`--person a --person b`) o va separado por coma. |
| `--all-persons`        | Los eventos de todas las personas. Es el comportamiento sin `--person`.                       |

```text
$ fiscalapi webhooks create --url https://example.com/webhooks --events '*' --description Producción
ID           6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Kind         url
Status       enabled
URL          https://example.com/webhooks
Description  Producción
Events       *
Persons      all
Created      2026-10-13T09:30:00.000
Updated      2026-10-13T09:30:00.000

Signing secret: whsec_<secreto>
Save this signing secret. Use it to verify that events come from FiscalAPI.
```

El secreto de firma se muestra una sola vez: `list` y `retrieve` nunca lo muestran. Guárdalo en ese momento en la
configuración de tu servidor; si lo pierdes, genera otro con `rotate-secret`. La línea `Signing secret:` va a stdout y
el aviso de guardarlo, a stderr.

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
Error: --person needs at least one person ID. Use --all-persons to receive events for all persons.
Run `fiscalapi --help` to see commands and options.
```

`--person` y `--all-persons` no se combinan.

### Actualizar un webhook

`update` lee el webhook, cambia solo lo que indicas y lo guarda. Acepta las opciones de `create`, todas opcionales, y
además:

| Opción       | Descripción                                                           |
| ------------ | --------------------------------------------------------------------- |
| `--enabled`  | Habilita el webhook y limpia el motivo y la fecha de deshabilitación. |
| `--disabled` | Deshabilita el webhook y cierra sus entregas pendientes.              |

```bash
fiscalapi webhooks update 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --events invoice.canceled,invoice.cancellation_rejected
fiscalapi webhooks update 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --disabled
```

Imprime el webhook actualizado, como `retrieve`. Estos casos fallan:

| Caso                           | Salida | Error                                                                                                              |
| ------------------------------ | ------ | ------------------------------------------------------------------------------------------------------------------ |
| Sin ninguna opción             | 2      | `Error: Specify what to update: --url, --description, --events, --person, --all-persons, --enabled or --disabled.` |
| `--enabled` con `--disabled`   | 2      | `Error: --enabled cannot be used with --disabled.`                                                                 |
| El id es tu sesión de `listen` | 1      | ``Error: A listen session cannot be updated. Use `fiscalapi listen` and `fiscalapi trigger` instead.``             |

### Eliminar un webhook

En una terminal, `delete` pide confirmación. Solo `y` o `yes` confirman:

```text
$ fiscalapi webhooks delete 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Delete webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d? Its pending deliveries will be closed. [y/N] y
Webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d deleted.
```

Cualquier otra respuesta imprime `Webhook <id> was not deleted.` y sale con 0. En un script (stdin redirigido) o con
`--json` no hay a quién preguntar: agrega `--yes`, o sale con 2.

```text
$ fiscalapi webhooks delete 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d --json
Error: Use --yes to delete without an interactive confirmation.
```

### Rotar el secreto de firma

```text
$ fiscalapi webhooks rotate-secret 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
ID           6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Kind         url
Status       enabled
URL          https://example.com/webhooks
Description  Producción
Events       *
Persons      all
Created      2026-10-13T09:30:00.000
Updated      2026-10-14T11:05:00.000

Signing secret: whsec_<secreto>
Save this signing secret. Use it to verify that events come from FiscalAPI.
The old signing secret also signs events for 24 hours, so you can update your server.
```

Durante esas 24 horas configura tu servidor con los dos secretos de firma y quita el anterior al terminar. Los detalles
de la rotación están en la [guía de webhooks](https://docs.fiscalapi.com/webhooks-info). `rotate-secret` también
funciona con el id de tu sesión de `listen`.

## `fiscalapi events`

| Comando                   | Qué hace                                                              |
| ------------------------- | --------------------------------------------------------------------- |
| `events list`             | Lista los eventos de los últimos 30 días, del más nuevo al más viejo. |
| `events retrieve <id>`    | Imprime el evento tal como se entregó.                                |
| `events resend <eventId>` | Reenvía un evento a un webhook o a tu sesión de `listen`.             |

### Listar eventos

| Opción                     | Descripción                                                                                   |
| -------------------------- | --------------------------------------------------------------------------------------------- |
| `--type <type>`            | Solo los eventos de este tipo, por ejemplo `invoice.canceled` o `webhook.test`.               |
| `--related-object-id <id>` | Solo los eventos de este recurso (`data.relatedObject.id`), por ejemplo el id de una factura. |
| `--from <date>`            | Desde este instante, incluido.                                                                |
| `--to <date>`              | Hasta este instante, incluido. Por omisión, ahora.                                            |
| `--page`, `--page-size`    | Ver [Paginación](#paginación).                                                                |

```text
$ fiscalapi events list
ID                                    TYPE              CREATED (UTC)         RESOURCE                              PERSON
0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f  invoice.canceled  2026-10-12T15:47:46Z  c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f  2e7d4c1b-9a8f-4e6d-b5c4-3a2b1c0d9e8f
5e8a7b6c-3d2f-4a1e-9b0c-8d7e6f5a4b3c  webhook.test      2026-10-12T15:40:00Z  6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d  -
Page 1 of 1 (2 total).
```

```bash
# Las cancelaciones de una factura en octubre
fiscalapi events list --type invoice.canceled --related-object-id c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f --from 2026-10-01 --to 2026-10-31
```

### Fechas

`--from` y `--to` aceptan ISO 8601:

| Escribes                                   | Significa                                                                                            |
| ------------------------------------------ | ---------------------------------------------------------------------------------------------------- |
| `2026-10-01`                               | Con `--from`, el inicio del día en UTC. Con `--to`, el día completo, hasta las 23:59:59.9999999 UTC. |
| `2026-10-01T08:00` o `2026-10-01T08:00:00` | Ese instante en UTC.                                                                                 |
| `2026-10-01T08:00:00-06:00`                | Ese instante en esa zona horaria.                                                                    |

Sin zona, la hora se toma como UTC. Otro formato es un error de uso (salida 2):

```text
$ fiscalapi events list --from ayer
Error: --from must be an ISO 8601 date, e.g. 2026-10-01 or 2026-10-01T08:00:00-06:00. Got 'ayer'.
Run `fiscalapi --help` to see commands and options.
```

### Ver un evento

`events retrieve` imprime el evento como lo devuelve la API, con o sin `--json`. Su `id` es el del header `webhook-id`
de sus entregas. La API lo devuelve indentado y el cuerpo que recibe tu servidor va compacto, así que esta salida no
sirve para verificar una firma: verifica con el cuerpo crudo que recibió tu servidor.

```text
$ fiscalapi events retrieve 0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f
{
    "id": "0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f",
    "type": "invoice.canceled",
    "createdAt": "2026-10-12T15:47:46Z",
    "personId": "2e7d4c1b-9a8f-4e6d-b5c4-3a2b1c0d9e8f",
    "data": {
      "relatedObject": {
        "id": "c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f",
        "type": "invoice",
        "url": "/api/v4/invoices/c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f"
      },
      "status": "X",
      "previousStatus": "C"
    }
  }
```

### Reenviar un evento

```text
$ fiscalapi events resend 0d6f5c1e-8a2b-4c3d-9e8f-7a6b5c4d3e2f --webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Resend created: delivery d1e2f3a4-b5c6-4d7e-8f9a-0b1c2d3e4f5a to webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d.
```

| Opción           | Descripción                                                                                |
| ---------------- | ------------------------------------------------------------------------------------------ |
| `--webhook <id>` | Webhook que recibe el reenvío. Por omisión, tu sesión de `listen`, que debe estar abierta. |

Sin `--webhook` recibes en tu equipo un evento que ocurrió mientras `listen` no corría. Si no hay una sesión de
`listen` abierta, la API responde 409 ([Solución de problemas](solucion-de-problemas.md#events-resend-sin---webhook-falla)).

El reenvío crea una entrega nueva del mismo evento, con el mismo `webhook-id` y el mismo cuerpo. Tu servidor debe
descartar los ids que ya procesó.

## `--json`

Con `--json`, cada comando imprime en stdout el `data` de la respuesta de la API, sin cambios:

```bash
# Los ids de los webhooks habilitados
fiscalapi webhooks list --page-size 50 --json | jq -r '.items[] | select(.status == "enabled") | .id'

# El secreto de firma de un webhook nuevo, directo a una variable
secret=$(fiscalapi webhooks create --url https://example.com/webhooks --events '*' --json | jq -r .secret)
```

Una página trae `items`, `pageNumber`, `totalPages`, `totalCount`, `hasPreviousPage` y `hasNextPage`. El aviso de
guardar el secreto de firma y los errores siguen en stderr como texto. Más en [Salida y códigos](salida-y-codigos.md).

Siguiente: [`fiscalapi trigger`](trigger.md).
