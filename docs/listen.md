# `fiscalapi listen`

Recibe en tu equipo los eventos de webhook de tu cuenta y los reenvía a tu servidor local, sin exponerlo a internet.

## Uso

```text
fiscalapi listen [--forward-to <url>] [--events <tipos>] [--headers "K:V,K:V"] [--skip-verify] [--print-secret]
```

Requiere una API key con el permiso `Webhooks.Manage` ([Autenticación](autenticacion.md)).

## Opciones

| Opción | Descripción |
|---|---|
| `--forward-to <url>` | URL absoluta `http` o `https` de tu servidor, p. ej. `http://localhost:3000/webhooks`. Sin ella, `listen` solo muestra los eventos. |
| `--events <tipos>` | Tipos separados por coma, p. ej. `invoice.canceled,invoice.cancellation_rejected`. Por omisión, `*` (todos). |
| `--headers "K:V,K:V"` | Headers extra en cada reenvío. El valor llega hasta la coma siguiente y puede llevar `:`. |
| `--skip-verify` | No verifica el certificado TLS de `--forward-to` (p. ej. uno autofirmado). **Nunca** afecta a la conexión con la API. |
| `--print-secret` | Imprime solo el secreto de firma en stdout y termina. |

También acepta las opciones globales `--live`, `--api-base`, `--json` y `--verbose`
([Salida y códigos](salida-y-codigos.md)).

Estas combinaciones son errores de uso (salida 2):

| Caso | Error |
|---|---|
| `--headers` o `--skip-verify` sin `--forward-to` | `Error: --skip-verify solo aplica con --forward-to.` |
| `--headers` con un header firmado | `Error: --headers no puede cambiar webhook-id: lo firma FiscalAPI.` |
| `--print-secret` con `--forward-to`, `--headers` o `--skip-verify` | `Error: --print-secret no se combina con --forward-to.` |

> **`--print-secret` abre una sesión**, igual que `listen`. Si ya corre un `fiscalapi listen` con tu misma persona, lo
> reemplaza y aquel termina con el [409](#conexión-y-sesiones). Úsalo antes de arrancar `listen`.

## Ejemplos

Reenvía los eventos a tu servidor local:

```text
$ fiscalapi listen --forward-to http://localhost:3000/webhooks
Listo. Tu secreto de firma es whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw (escucha 9b2e7c4a-1d3f-4e6b-8a5c-0f1e2d3c4b5a)
2026-10-12 09:47:46  --> invoice.canceled [3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b] [200]
```

Cada línea trae la hora local, el tipo, el id del evento (el header `webhook-id`) y lo que respondió tu servidor. Para
disparar un evento de prueba, corre [`fiscalapi trigger`](trigger.md) en otra terminal.

```bash
# Solo muestra las cancelaciones, sin reenviarlas
fiscalapi listen --events invoice.canceled,invoice.cancellation_rejected

# Servidor con TLS autofirmado y un header de autorización propio
fiscalapi listen --forward-to https://localhost:5443/webhooks --skip-verify --headers "Authorization:Bearer token-local"

# Guarda el secreto para configurar tu servidor
export FISCALAPI_WEBHOOK_SECRET=$(fiscalapi listen --print-secret)
```

## Cómo funciona

1. `listen` abre **tu escucha de CLI**: un webhook sin URL, uno por persona, con su propio secreto de firma
   (`POST /api/v4/webhooks/cli-listener`). El secreto no cambia entre reinicios: configúralo una vez.
2. Cada evento de tu cuenta de los tipos de `--events` genera una entrega para tu escucha. `listen` las pide por
   long-polling (`GET /api/v4/webhooks/{id}/cli-deliveries`, hasta 25 s por petición).
3. Reenvía cada entrega a `--forward-to` como un `POST` con el cuerpo **exacto** que firmó FiscalAPI
   (`Content-Type: application/json; charset=utf-8`), los headers firmados `webhook-id`, `webhook-timestamp` y
   `webhook-signature`, y los de `--headers`.
4. Manda a la API un acuse con el resultado, así la entrega queda en el historial como la de un webhook con URL.

Se reenvían hasta 16 entregas a la vez; las de un mismo evento conservan su orden. Las redirecciones no se siguen.

**No hay reintentos.** Si tu servidor falla, la entrega queda con ese resultado; repítela con
[`fiscalapi events resend`](webhooks-y-eventos.md#reenviar-un-evento).

## Resultado de cada reenvío

| Final de la línea | Significado |
|---|---|
| `[200]` | Tu servidor respondió ese código. Cualquier código de 100 a 599 se muestra y se acusa tal cual. |
| `[error: Timeout]` | Tu servidor no respondió en 15 s. |
| `[error: ConnectionFailed]` | No se pudo conectar: nada escucha en ese puerto, DNS o conexión rechazada. |
| `[error: TlsFailed]` | Falló el TLS hacia `--forward-to`, p. ej. un certificado autofirmado sin `--skip-verify`. |
| `[error: InvalidResponse]` | La respuesta no es HTTP válido o se cortó. |
| (nada) | Sin `--forward-to`: el evento solo se mostró y se acusa como entregado. |

Son los mismos nombres de error del historial de entregas de la API.

## Verificar la firma

Cada reenvío lleva la firma de FiscalAPI con el secreto de tu escucha (`whsec_…`), según
[Standard Webhooks](https://www.standardwebhooks.com/). Verifícala como la de un webhook con URL, con el **cuerpo
crudo** tal como llegó. Si lo deserializas y lo vuelves a serializar, cambian sus bytes y la firma no coincide.

Los SDK oficiales traen la verificación:

| SDK | Función |
|---|---|
| .NET (`Fiscalapi`) | `Fiscalapi.Webhooks.Webhook.Verify(headers, body, secret)` |
| Node.js (`fiscalapi`) | `Webhook.verify(headers, body, secret)` |
| Python (`fiscalapi`) | `fiscalapi.webhooks.verify(headers, body, secret)` |
| PHP | `Fiscalapi\Webhooks\Webhook::verify(...)` |
| Java | `com.fiscalapi.webhooks.Webhook.verify(headers, body, secret)` |

Todas comparan la firma en tiempo constante, rechazan un `webhook-timestamp` con más de 5 minutos de diferencia y
devuelven el evento verificado. En ASP.NET Core:

```csharp
[HttpPost("webhooks/fiscalapi")]
public async Task<IActionResult> Receive()
{
    string body;
    using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        body = await reader.ReadToEndAsync();

    WebhookEvent webhookEvent;
    try
    {
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        webhookEvent = Webhook.Verify(headers, body, _webhookSecret);
    }
    catch (WebhookVerificationException)
    {
        return BadRequest(); // firma inválida, header faltante o timestamp fuera de tolerancia
    }

    // Responde 2xx rápido y procesa después.
    return Ok();
}
```

Para probar tu verificación sin la API, usa el vector oficial de Standard Webhooks. Con la hora actual igual al
timestamp, la firma verifica; si cambias un carácter del cuerpo, de la firma o del secreto, falla.

| Dato | Valor |
|---|---|
| Secreto | `whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw` |
| `webhook-id` | `msg_p5jXN8AQM9LWM0D4loKWxJek` |
| `webhook-timestamp` | `1614265330` |
| Cuerpo | `{"test": 2432232314}` |
| `webhook-signature` | `v1,g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=` |

La forma del evento y la verificación a mano en cada lenguaje están en la
[guía de webhooks de FiscalAPI](https://docs.fiscalapi.com/webhooks).

## Conexión y sesiones

`listen` corre hasta que lo detienes. Si la primera apertura de la escucha falla (p. ej. un `--api-base` equivocado o
una API key sin permiso), termina de inmediato con el error. Después:

| Situación | Qué hace `listen` |
|---|---|
| Red caída, 408, 429 o 5xx | Reintenta con espera creciente, de 1 s hasta 30 s con variación aleatoria, y lo avisa en stderr. |
| 410, sesión caducada | La API cierra la sesión tras 60 s sin long-polling (un corte largo, el equipo suspendido). `listen` abre una nueva y sigue. |
| 409, sesión reemplazada | Otro `listen` o un `--print-secret` con tu misma persona abrió sesión. Solo puede haber una: esta termina con 1. |
| Otro error de la API (401, 403, 404…) | Termina con 1 y el error de la API. |
| Ctrl+C | Acusa durante 2 s lo que ya tenía resultado y sale con **130**. Lo que estaba a medio reenviar queda sin acuse y la API lo cierra. |

```text
Se perdió la conexión con FiscalAPI (No se pudo conectar con https://test.fiscalapi.com/: …); reintentando en 2 s.
Conexión con FiscalAPI restablecida.
La sesión de fiscalapi listen caducó; se abre una nueva.
```

Los eventos que ocurren sin sesión abierta **no** llegan después. Búscalos con `fiscalapi events list` y reenvíalos
con `fiscalapi events resend <eventId>`.

Una sesión reemplazada termina así:

```text
Error: Sorry, it's not me, it's you.
  Detalle: Otra sesión de fiscalapi listen reemplazó a esta.
  Código HTTP: 409
  traceIdentifier (para soporte): …
```

Otros avisos en stderr:

| Aviso | Qué pasó |
|---|---|
| `La entrega <deliveryId> (<eventId>) venció antes de reenviarse; la API la cerró sin acuse.` | La entrega esperó su turno más de 2 minutos y la API ya la cerró: no se reenvía. |
| `No se registró el acuse de la entrega <deliveryId>: …` | La API rechazó el acuse. Un 409 (la entrega ya no esperaba acuse) solo se muestra con `--verbose`. |
| `No se pudo reabrir la sesión de escucha (…); reintentando en <n> s.` | Al reabrir tras un 410, la API respondió 409 porque otra apertura de tu persona estaba en curso. Se reintenta con espera creciente. |

## Salida con `--json`

Con `--json`, stdout lleva **un objeto JSON por entrega**, uno por línea (JSON Lines). Los avisos, los errores y la
línea `Listo. Tu secreto de firma es …` siguen en stderr como texto.

```json
{"time":"2026-10-12T09:47:46.1234567-06:00","deliveryId":"a7b8c9d0-e1f2-4a3b-8c4d-5e6f7a8b9c0d","eventId":"3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b","type":"invoice.canceled","statusCode":200,"latencyMs":12,"event":{"id":"3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b","type":"invoice.canceled","createdAt":"2026-10-12T15:47:46Z","personId":null,"data":{"relatedObject":{"id":"c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f","type":"invoice","url":"/api/v4/invoices/c4d3e2f1-a0b9-4c8d-9e7f-6a5b4c3d2e1f"},"status":"X","previousStatus":"C"}}}
```

| Campo | Contenido |
|---|---|
| `time` | Cuándo terminó el reenvío, en hora local con su desfase. |
| `deliveryId` | Id de la entrega. |
| `eventId` | El header `webhook-id`, o `?` si no vino. |
| `type` | El tipo del evento, o `?` si el cuerpo no es un evento. |
| `statusCode` | Lo que respondió tu servidor. Solo con `--forward-to` y si respondió. |
| `latencyMs` | Lo que tardó el reenvío. Solo con `--forward-to`. |
| `error` | `Timeout`, `ConnectionFailed`, `TlsFailed` o `InvalidResponse`. Solo con `--forward-to` y si no hubo respuesta. |
| `event` | El evento tal como llegó. Se omite si el cuerpo no es JSON. |

Con `--print-secret --json`, la única línea es `{"secret":"whsec_…"}`.

```bash
# El tipo y el resultado de cada reenvío
fiscalapi listen --forward-to http://localhost:3000/webhooks --json | jq -r '"\(.type) \(.statusCode // .error)"'
```

## Docker y terminales sin ConPTY

Dentro de un contenedor, `http://localhost:…` es el propio contenedor. Para llegar a un servidor del equipo anfitrión
usa su nombre de red (en Docker Desktop, `http://host.docker.internal:3000/webhooks`) y pasa las credenciales con
`-e FISCALAPI_API_KEY -e FISCALAPI_TENANT_KEY`.

`listen` funciona en cualquier terminal. Solo `login` necesita una terminal real para ocultar las llaves
([Autenticación](autenticacion.md#sin-terminal-stdin)).

Siguiente: [Webhooks y eventos](webhooks-y-eventos.md).
