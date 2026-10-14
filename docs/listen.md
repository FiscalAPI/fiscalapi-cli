# `fiscalapi listen`

`listen` recibe en tu equipo los eventos de webhook de tu cuenta y los reenvía a tu servidor local, sin exponerlo a
internet.

```bash
fiscalapi listen --forward-to http://localhost:3000/webhooks
```

Requiere una API key cuya persona tenga el permiso `Webhooks.Manage` ([Autenticación](autenticacion.md)).

## Cómo funciona

1. `listen` abre (o renueva) **tu escucha de CLI**: un webhook sin URL, uno por persona, con su propio secreto de firma
   (`POST /api/v4/webhooks/cli-listener`). Escribe en stderr:

   ```text
   Listo. Tu secreto de firma es whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw (escucha wh_listener_0001)
   ```

   El secreto es estable entre reinicios de `listen`: configúralo una vez en tu servidor local.

2. Mientras la sesión está abierta, cada evento de tu cuenta de los tipos de `--events` genera una entrega para tu
   escucha. `listen` las pide por long-polling (`GET /api/v4/webhooks/{id}/cli-deliveries`, espera de hasta 25 s).

3. Cada entrega se reenvía a `--forward-to` como un `POST` con:
   - el cuerpo **exacto** que firmó FiscalAPI, byte a byte, con `Content-Type: application/json; charset=utf-8`;
   - los headers firmados `webhook-id`, `webhook-timestamp` y `webhook-signature` (Standard Webhooks);
   - los headers extra de `--headers`.

4. `listen` manda a la API un acuse con lo que respondió tu servidor (el código HTTP o el error), así la entrega queda en
   el historial igual que la de un webhook con URL.

5. Por cada entrega escribe una línea en stdout:

   ```text
   2026-10-12 09:47:46  --> invoice.canceled [evt_0001] [200]
   ```

   Es la hora local, el tipo, el id del evento (el header `webhook-id`) y el resultado del reenvío.

Una entrega lenta no frena a las demás: se reenvían hasta 16 a la vez, y las de un mismo evento conservan su orden.
**No hay reintentos**: si tu servidor responde un error, la entrega queda con ese resultado; vuelve a pedirla con
[`fiscalapi events resend`](webhooks-y-eventos.md#reenviar-un-evento).

Para disparar un evento de prueba hacia tu escucha, usa [`fiscalapi trigger`](trigger.md) en otra terminal. Ese
evento llega aunque `--events` no incluya su tipo.

## Opciones

| Opción | Uso |
|---|---|
| `--forward-to <url>` | URL absoluta `http` o `https` de tu servidor (p. ej. `http://localhost:3000/webhooks`). Sin ella, `listen` solo muestra los eventos. |
| `--events <tipos>` | Tipos separados por coma (p. ej. `invoice.canceled,invoice.cancellation_rejected`). Por omisión, `*` (todos). |
| `--headers "K:V,K:V"` | Headers extra en cada reenvío. El valor llega hasta la coma siguiente y puede llevar `:`. |
| `--skip-verify` | No verifica el certificado TLS de `--forward-to` (p. ej. uno autofirmado). **Nunca** afecta a la conexión con la API. |
| `--print-secret` | Imprime solo el secreto de firma en stdout y termina. |

Más las opciones globales `--live`, `--api-base`, `--json` y `--verbose` ([Salida y códigos](salida-y-codigos.md)).

Ejemplos:

```bash
# Solo ver los eventos de cancelación, sin reenviarlos
fiscalapi listen --events invoice.canceled,invoice.cancellation_rejected

# Reenviar a un servidor con TLS autofirmado y un header de autorización propio
fiscalapi listen --forward-to https://localhost:5443/webhooks --skip-verify --headers "Authorization:Bearer token-local"

# Guardar el secreto para la configuración del servidor local
export FISCALAPI_WEBHOOK_SECRET=$(fiscalapi listen --print-secret)
```

Reglas de las opciones (son errores de uso, salida 2):

- `--headers` y `--skip-verify` solo aplican con `--forward-to`: `Error: --skip-verify solo aplica con --forward-to.`
- `--headers` no puede cambiar un header firmado: `Error: --headers no puede cambiar webhook-id: lo firma FiscalAPI.`
- `--print-secret` no se combina con las opciones del reenvío: `Error: --print-secret no se combina con --forward-to.`

> **Atención con `--print-secret`:** abre una sesión, igual que `listen`. Si ya tienes un `fiscalapi listen` corriendo
> con la misma API key, esa sesión queda reemplazada y aquel `listen` termina con el 409 de abajo. Usa `--print-secret`
> antes de arrancar `listen`, no mientras corre.

## El resultado de cada reenvío

| Línea | Significado |
|---|---|
| `... [200]` | Tu servidor respondió ese código. Cualquier código de 100 a 599 se muestra y se acusa tal cual; las redirecciones no se siguen. |
| `... [error: Timeout]` | Tu servidor no respondió en 15 segundos. |
| `... [error: ConnectionFailed]` | No se pudo conectar (nada escucha en ese puerto, DNS, conexión rechazada). |
| `... [error: TlsFailed]` | Falló el TLS hacia `--forward-to` (p. ej. un certificado autofirmado sin `--skip-verify`). |
| `... [error: InvalidResponse]` | La respuesta no es HTTP válido o se cortó. |
| `...` sin resultado | Sin `--forward-to`: el evento solo se mostró (se acusa como entregado). |

Los nombres de los errores son los mismos del historial de entregas de la API.

## Verificar la firma en tu servidor local

Cada reenvío lleva la firma de FiscalAPI con el secreto de tu escucha (`whsec_…`), según
[Standard Webhooks](https://www.standardwebhooks.com/). Tu servidor la verifica igual que la de un webhook con URL:
con el **cuerpo crudo** tal como llegó, antes de deserializarlo. Un cuerpo deserializado y vuelto a serializar cambia
sus bytes (espacios, orden, escapes) y la firma deja de coincidir.

Los SDK oficiales traen la verificación:

| SDK | Función |
|---|---|
| .NET (`Fiscalapi`) | `Fiscalapi.Webhooks.Webhook.Verify(headers, body, secret)` |
| Node.js (`fiscalapi`) | `Webhook.verify(headers, body, secret)` |
| Python (`fiscalapi`) | `fiscalapi.webhooks.verify(headers, body, secret)` |
| PHP | `Fiscalapi\Webhooks\Webhook::verify(...)` |
| Java | `com.fiscalapi.webhooks.Webhook.verify(headers, body, secret)` |

Las cinco comprueban la firma en tiempo constante, rechazan un `webhook-timestamp` con más de 5 minutos de diferencia y
devuelven el evento ya verificado. Por ejemplo, en ASP.NET Core (del README del SDK de .NET):

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

Para probar tu verificación sin la API, usa el vector oficial de Standard Webhooks: con estos valores la firma verifica
si pasas como hora actual el mismo timestamp, y falla si cambias un carácter del cuerpo, de la firma o del secreto.

| Dato | Valor |
|---|---|
| Secreto | `whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw` |
| `webhook-id` | `msg_p5jXN8AQM9LWM0D4loKWxJek` |
| `webhook-timestamp` | `1614265330` |
| Cuerpo | `{"test": 2432232314}` |
| `webhook-signature` | `v1,g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=` |

La guía completa (forma del evento, verificación a mano en cada lenguaje, reintentos) está en la
[guía de webhooks de FiscalAPI](https://docs.fiscalapi.com/webhooks).

## Conexión, sesiones y salida

`listen` no termina por sí solo. Mientras corre:

- **Red caída o error 5xx de la API:** reintenta con espera creciente (de 1 s hasta 30 s, con variación aleatoria) y lo
  avisa en stderr:

  ```text
  Se perdió la conexión con FiscalAPI (No se pudo conectar con https://test.fiscalapi.com/: …); reintentando en 2 s.
  Conexión con FiscalAPI restablecida.
  ```

- **410, sesión caducada:** la API cierra la sesión si pasan 60 s sin long-polling (por ejemplo, tras un corte largo o
  si la computadora se suspendió). `listen` abre una nueva sin terminar:

  ```text
  La sesión de fiscalapi listen caducó; se abre una nueva.
  ```

  Los eventos que ocurrieron mientras no había sesión **no** llegan después. Búscalos con
  `fiscalapi events list` y reenvíalos con `fiscalapi events resend <eventId>`.

- **409, sesión reemplazada:** otra sesión con tu misma persona (otro `listen` o un `--print-secret`) reemplazó a esta.
  Solo puede haber una; esta termina con código 1:

  ```text
  Error: Sorry, it's not me, it's you.
    Detalle: Otra sesión de fiscalapi listen reemplazó a esta.
    Código HTTP: 409
    traceIdentifier (para soporte): …
  ```

- **401, 403 o 404:** no se resuelven reintentando; `listen` termina con código 1 y el error de la API.

- **Ctrl+C:** cierra la escucha. Acusa durante 2 s lo que ya tenía resultado y sale con **130**. Lo que estaba a medio
  reenviar queda sin acuse y la API lo cierra.

Avisos que puedes ver en stderr:

| Aviso | Qué pasó |
|---|---|
| `La entrega <deliveryId> (<eventId>) venció antes de reenviarse; la API la cerró sin acuse.` | La entrega esperó demasiado su turno y la API ya la había cerrado: no se reenvía. |
| `No se registró el acuse de la entrega <deliveryId>: …` | La API no aceptó el acuse. Un 409 en el acuse (una entrega que ya no espera acuse) solo se muestra con `--verbose`. |
| `No se pudo reabrir la sesión de escucha (…); reintentando en <n> s.` | Falló la reapertura tras un 410; se reintenta con espera creciente. |

## Salida con `--json`

Con `--json`, stdout lleva **un objeto JSON por línea** (JSON Lines), uno por entrega, para procesarlo con otra
herramienta. Los avisos y errores siguen en stderr como texto, incluida la línea `Listo. Tu secreto de firma es …`.

```json
{"time":"2026-10-12T09:47:46.1234567-06:00","deliveryId":"dlv_0001","eventId":"evt_0001","type":"invoice.canceled","statusCode":200,"latencyMs":12,"event":{"id":"evt_0001","type":"invoice.canceled","createdAt":"2026-10-12T18:47:53Z","personId":null,"data":{"relatedObject":{"id":"inv_1","type":"invoice","url":"/api/v4/invoices/inv_1"},"status":"X","previousStatus":"V"}}}
```

| Campo | Contenido |
|---|---|
| `time` | Cuándo terminó el reenvío, en hora local con su desfase. |
| `deliveryId` | Id de la entrega. |
| `eventId` | El header `webhook-id`, o `?` si no vino. |
| `type` | El tipo del evento, o `?` si el cuerpo no es un evento. |
| `statusCode` | Lo que respondió tu servidor. Solo con `--forward-to`, y solo si respondió. |
| `latencyMs` | Lo que tardó el reenvío. Solo con `--forward-to`. |
| `error` | `Timeout`, `ConnectionFailed`, `TlsFailed` o `InvalidResponse`. Solo con `--forward-to`, si no hubo respuesta. |
| `event` | El evento tal como llegó. Se omite si el cuerpo no es JSON. |

Sin `--forward-to` cada línea trae solo `time`, `deliveryId`, `eventId`, `type` y `event`. Con
`--print-secret --json` la única línea es `{"secret":"whsec_…"}`.

```bash
# Solo los tipos y códigos de cada reenvío
fiscalapi listen --forward-to http://localhost:3000/webhooks --json | jq -r '"\(.type) \(.statusCode // .error)"'
```

## Terminales sin ConPTY y Docker

`listen` funciona en cualquier terminal. Solo `login` depende de una terminal real para leer las llaves sin mostrarlas:
en mintty (Git Bash) o en `docker run -i` sin `-t`, `login` lee la entrada como redirigida, **sin prompt y con eco**
([Autenticación](autenticacion.md#sin-terminal-stdin)).

Dentro de un contenedor, `--forward-to http://localhost:…` apunta al propio contenedor. Para llegar a un servidor del
equipo anfitrión usa su nombre de red (en Docker Desktop, `http://host.docker.internal:3000/webhooks`) y pasa las
credenciales con `-e FISCALAPI_API_KEY -e FISCALAPI_TENANT_KEY`.

Siguiente: [Webhooks y eventos](webhooks-y-eventos.md).
