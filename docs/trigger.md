# `fiscalapi trigger`

Dispara un evento de prueba y lo entrega a tu sesión de [`fiscalapi listen`](listen.md) o a un webhook. Sirve para
probar tu servidor sin timbrar, cancelar ni descargar nada real. Solo funciona en el ambiente de pruebas.

## Uso

```text
fiscalapi trigger <type> [--related-object-id <id>] [--person-id <id>] [--webhook <id>]
```

Requiere una API key con el permiso `Webhooks.Manage` ([Autenticación](autenticacion.md)).

## Argumentos

| Argumento | Descripción                                                                                                                                                                                    |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `<type>`  | Tipo de evento: `invoice.cancellation_requested`, `invoice.canceled`, `invoice.cancellation_rejected`, `download_request.completed`, `download_request.abandoned` o `download_rule.suspended`. |

`fiscalapi trigger --help` lista los tipos. Quien valida el tipo es la API, así que un tipo nuevo funciona sin
actualizar el CLI. `webhook.test` no se dispara con `trigger`.

## Opciones

| Opción                     | Descripción                                                                                             |
| -------------------------- | ------------------------------------------------------------------------------------------------------- |
| `--related-object-id <id>` | Id del recurso del evento (`data.relatedObject.id`), hasta 36 caracteres. Por omisión, un id sintético. |
| `--person-id <id>`         | Persona de tu cuenta dueña del recurso (`personId`). Por omisión, el evento va sin persona.             |
| `--webhook <id>`           | Webhook que recibe el evento. Por omisión, tu sesión de `listen`, que debe estar abierta.               |

También acepta las opciones globales `--live`, `--api-base`, `--json` y `--verbose`
([Salida y códigos](salida-y-codigos.md)).

## Ejemplos

Dispara un evento hacia tu sesión de `listen`:

```text
# Terminal 1
$ fiscalapi listen --forward-to http://localhost:3000/webhooks
Ready. Your signing secret is whsec_<secreto> (listen session <id>).
2026-10-12 09:47:46  invoice.canceled [3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b] [200]

# Terminal 2
$ fiscalapi trigger invoice.canceled
Event triggered: 3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b to webhook 9b2e7c4a-1d3f-4e6b-8a5c-0f1e2d3c4b5a.
```

Dispara un evento hacia un webhook con URL, sin `listen`:

```text
$ fiscalapi trigger invoice.canceled --webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d
Event triggered: 3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b to webhook 6f1c2a4e-0b7d-4c55-9a3e-1d2f3a4b5c6d.
```

Usa un recurso y una persona reales de tu cuenta, para que tu servidor pueda consultarlos:

```bash
fiscalapi trigger download_request.completed \
  --related-object-id 8a1f0c2e-5d6b-4f7a-9c3e-1b2d3e4f5a6b \
  --person-id 2e7d4c1b-9a8f-4e6d-b5c4-3a2b1c0d9e8f
```

Con `--json`, imprime el `data` de la respuesta sin cambios:

```text
$ fiscalapi trigger invoice.canceled --json
{
    "eventId": "3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b",
    "webhookId": "9b2e7c4a-1d3f-4e6b-8a5c-0f1e2d3c4b5a"
  }
```

## Cómo se entrega

- El evento tiene la forma y la firma de uno real: el recurso, `status` y `previousStatus` de su tipo (por ejemplo,
  `invoice.canceled` pasa de `C` a `X`). Tu servidor lo verifica con el mismo código.
- Llega solo al destino indicado, aunque no esté suscrito a ese tipo ni a esa persona. Tus demás webhooks no lo
  reciben.
- Es un solo intento: si tu servidor falla, no se reintenta. Repítelo con `trigger` o con
  [`fiscalapi events resend <eventId>`](webhooks-y-eventos.md#reenviar-un-evento), con el mismo `--webhook` del
  `trigger`: sin él, el reenvío va a tu sesión de `listen`.
- Queda en `fiscalapi events list` como cualquier otro evento.

## Errores

Los errores de la API salen con código 1 y el texto en stderr. El mensaje y la línea `Details` los envía la API:

```text
$ fiscalapi trigger invoice.canceled --live
Error: Sorry, it's not me, it's you.
  Details: fiscalapi trigger no está disponible en producción; usa el ambiente de pruebas.
  HTTP status: 403
  traceIdentifier (for support): <traceIdentifier>
```

Un 400 de validación pone el texto general en `Details` y una línea `- <mensaje>` por cada regla que falló. Un tipo de
evento que no existe sale así:

```text
$ fiscalapi trigger invoice.cancelled
Error: Sorry, it's not me, it's you.
  Details: One or more validation failures have occurred.
  - El tipo de evento 'invoice.cancelled' no existe.
  HTTP status: 400
  traceIdentifier (for support): <traceIdentifier>
```

| Código | Texto de la API                                                                                         | Qué hacer                                                                                                       |
| ------ | ------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| 409    | `La escucha de CLI no está conectada; ejecuta fiscalapi listen.`                                        | Abre `fiscalapi listen` o usa `--webhook`.                                                                      |
| 400    | `El tipo de evento 'invoice.cancelled' no existe.`                                                      | Usa un tipo de la lista.                                                                                        |
| 400    | `El webhook está deshabilitado: habilítalo antes de probarlo.`                                          | `fiscalapi webhooks update <id> --enabled`.                                                                     |
| 400    | `La persona especificada no es válida o no pertenece a tu organización.`                                | Revisa `--person-id`.                                                                                           |
| 403    | `fiscalapi trigger no está disponible en producción; usa el ambiente de pruebas.`                       | Quita `--live`.                                                                                                 |
| 403    | `No tienes permiso para administrar webhooks.`                                                          | Usa una API key con el permiso `Webhooks.Manage` ([detalles](solucion-de-problemas.md#403-sin-webhooksmanage)). |
| 404    | `No such resource for the given Id: <id>`                                                               | Revisa `--webhook`: no existe, se eliminó o no es de tu cuenta.                                                 |
| 429    | `Se excedió el límite de solicitudes de la familia BusinessWrite (<detalle>). Reintenta en N segundos.` | Espera los segundos que indica y repite.                                                                        |

Más casos en [Solución de problemas](solucion-de-problemas.md#fiscalapi-trigger).

Siguiente: [Salida y códigos de salida](salida-y-codigos.md).
