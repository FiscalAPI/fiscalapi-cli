# `fiscalapi trigger`

> **Disponible desde la versión que lo incluya.** Esta página describe la sintaxis prevista; el comando todavía no está
> en el CLI. Revisa `fiscalapi --help` en tu versión.

`trigger` le pide a FiscalAPI que genere un evento de prueba y lo entregue a un solo destino: tu escucha de
[`fiscalapi listen`](listen.md) o el webhook que indiques. Es el equivalente de
[`stripe trigger`](https://docs.stripe.com/cli/trigger).

```bash
# Terminal 1
fiscalapi listen --forward-to http://localhost:3000/webhooks

# Terminal 2
fiscalapi trigger invoice.canceled
```

## Sintaxis prevista

```text
fiscalapi trigger <type> [--related-object-id <id>] [--person-id <id>] [--webhook <id>]
```

| Argumento u opción | Uso |
|---|---|
| `<type>` | Tipo de evento: `invoice.cancellation_requested`, `invoice.canceled`, `invoice.cancellation_rejected`, `download_request.completed`, `download_request.abandoned` o `download_rule.suspended`. Quien valida es la API. |
| `--related-object-id <id>` | Id del recurso que irá en `data.relatedObject.id`. Sin él, la API usa un id sintético cuyo `GET` responde 404. |
| `--person-id <id>` | Persona del evento (`personId`). |
| `--webhook <id>` | Entrega el evento a ese webhook. Sin esta opción, va a tu escucha de CLI: `fiscalapi listen` tiene que estar abierto. |

Llama a `POST /api/v4/events/trigger` e imprime el id del evento creado. El evento existe después en
`fiscalapi events list` y se puede reenviar con `fiscalapi events resend`.

## Solo en pruebas

La API solo genera eventos de prueba en el ambiente de pruebas (`https://test.fiscalapi.com`). Con `--live` el CLI no
lo bloquea por su cuenta: muestra el 403 que responde la API y sale con 1.

Siguiente: [Salida y códigos de salida](salida-y-codigos.md).
