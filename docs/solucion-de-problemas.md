# Solución de problemas

Cada caso trae el texto que muestra el CLI o la API, la causa y el remedio. Para ver el detalle de una llamada a la
API, repite el comando con `--verbose` ([Salida y códigos](salida-y-codigos.md#--verbose)); nunca muestra tu API key
ni tu tenant key.

## Credenciales

### 401 `Invalid tenant key`

```text
Error: Sorry, it's not me, it's you.
  Details: Invalid tenant key
  HTTP status: 401
  traceIdentifier (for support): <traceIdentifier>
  Hint: Run `fiscalapi login` or set FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY.
```

**Causa:** la tenant key no existe en ese ambiente, o la API key y la tenant key son de ambientes distintos (las de
pruebas no sirven con `--live` y al revés).
**Remedio:** revisa la tenant key en el dashboard del ambiente correcto y vuelve a correr `fiscalapi login` (o
`fiscalapi login --live`). Si usas variables de entorno, recuerda que ganan sobre lo guardado.

### 401 `Missing required http X-API-KEY.`

**Causa:** la petición llegó sin el header `X-API-KEY`.
**Remedio:** el mismo del caso anterior.

### 401 `Invalid API KEY`

**Causa:** la API key no es válida para ese ambiente (revocada, mal copiada o de otro ambiente).
**Remedio:** el mismo del primer caso. `fiscalapi login` valida la API key y la tenant key antes de guardarlas:
`Error: Invalid credentials for test.fiscalapi.com.`

### 403 sin `Webhooks.Manage`

```text
Error: Sorry, it's not me, it's you.
  Details: No tienes permiso para administrar webhooks.
  HTTP status: 403
  traceIdentifier (for support): <traceIdentifier>
```

**Causa:** la persona de la API key no tiene el permiso `Webhooks.Manage`, que piden `listen`, `trigger`, `webhooks` y
`events`. `login` ya lo había advertido:
`This API key lacks the Webhooks.Manage permission. listen, trigger and webhooks will return 403.`
**Remedio:** pide al administrador de la cuenta que dé el permiso a esa persona, o usa la API key de una persona que lo
tenga.

### `No credentials for`

```text
Error: No credentials for test.fiscalapi.com. Run `fiscalapi login` or set FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY.
```

**Causa:** no hay variables de entorno ni nada guardado para ese ambiente. Las credenciales se guardan por host: haber
hecho `login` en pruebas no sirve para `--live` ni para otro `--api-base`.
**Remedio:** `fiscalapi login` con las mismas opciones de ambiente que usa el comando.

### `Set both FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY`

```text
Error: Set both FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY; only FISCALAPI_API_KEY is set.
```

**Causa:** solo una de las dos variables está definida. **Remedio:** define las dos o ninguna.

### `No credential store is available`

```text
Error: No credential store is available. Set FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY.
  Details: <motivo>
```

**Causa:** Linux sin Secret Service: WSL, un servidor sin sesión gráfica o un contenedor.
**Remedio:** usa las variables de entorno, o instala GNOME Keyring o un servicio compatible
([Autenticación](autenticacion.md#wsl-servidores-y-contenedores)).

### `The credential store is locked or not responding`

```text
Error: The credential store is locked or not responding. Unlock it and try again.
  Details: <motivo>
```

**Causa:** el llavero de macOS o GNOME Keyring está bloqueado, o no respondió a tiempo.
**Remedio:** desbloquéalo y repite el comando, o usa las variables de entorno.

### `login` muestra lo que escribo

**Causa:** la terminal no es un TTY (mintty de Git Bash) o es `docker run -i` sin `-t`; `login` trata la entrada como
redirigida, sin prompt y con eco.
**Remedio:** usa Windows Terminal o PowerShell, `docker run -it`, o las variables de entorno.

## Opciones y URLs

### `--api-base allows http only for localhost`

```text
Error: --api-base allows http only for localhost, 127.0.0.1 or ::1. Use https for 'http://fiscalapi.example.com'.
Run `fiscalapi --help` to see commands and options.
```

**Causa:** la API key y la tenant key viajan en headers; sin TLS solo se permite hablar con tu propio equipo.
**Remedio:** usa `https://`. Para una API local, `http://localhost:<puerto>` sí está permitido.

### Opciones que no se combinan

Son errores de uso (salida 2) y dicen qué corregir, por ejemplo:

```text
Error: --skip-verify requires --forward-to.
Error: --print-secret cannot be used with --forward-to.
Error: --person needs at least one person ID. Use --all-persons to receive events for all persons.
Error: Use --yes to delete without an interactive confirmation.
```

`fiscalapi <comando> --help` lista las opciones de cada comando.

## `fiscalapi listen`

### 409 al reemplazar la sesión de `listen`

**Causa:** abriste otro `fiscalapi listen` o un `fiscalapi listen --print-secret` con la misma API key (en otra
terminal, otra máquina o un script). Solo puede haber una sesión de `listen` a la vez; la anterior termina con código
1 ([ejemplo](listen.md#conexión-y-sesiones)).
**Remedio:** cierra la otra y vuelve a abrir `listen`. Corre `--print-secret` antes de `listen`, no mientras corre.

### Tu servidor no responde: `[error: Timeout]`, `[error: ConnectionFailed]`

```text
2026-10-12 09:47:46  invoice.canceled [3f9c1d2e-7b4a-4e8f-9a1b-2c3d4e5f6a7b] [error: ConnectionFailed]
```

**Causa:** nada escucha en la URL de `--forward-to` (puerto equivocado, servidor apagado, ruta en otro host), o tu
servidor tardó más de 15 s (`Timeout`). Dentro de un contenedor, `localhost` es el propio contenedor.
**Remedio:** prueba la URL con `curl -X POST <url>`; responde 2xx rápido y procesa después. Con un certificado
autofirmado verás `[error: TlsFailed]`: agrega `--skip-verify`. La entrega no se reintenta; pídela otra vez con
`fiscalapi events resend <eventId>`.

### La firma no verifica en tu servidor

**Causa más común:** tu servidor verifica un cuerpo re-serializado. Si tu framework deserializa el JSON y lo vuelves a
serializar, cambian los espacios, el orden o los escapes, y la firma ya no coincide. `listen` reenvía los bytes exactos
que firmó FiscalAPI.
**Remedio:** lee el cuerpo crudo antes de deserializarlo y pásalo a la función de verificación del SDK
([Verificar la firma](listen.md#verificar-la-firma)).

Otras causas:

- **Otro secreto de firma:** el de tu sesión de `listen` no es el de tus webhooks con URL. Toma el de la línea
  `Ready. Your signing secret is` o el de `fiscalapi listen --print-secret`, completo y con su prefijo `whsec_`.
- **Reloj desfasado:** los SDK rechazan un `webhook-timestamp` con más de 5 minutos de diferencia. Sincroniza la hora
  del equipo.
- **Rotaste el secreto de firma:** durante 24 horas configura los dos secretos de firma.
- **Headers alterados:** un proxy local que cambia `webhook-id`, `webhook-timestamp` o `webhook-signature`. `--headers`
  no puede cambiarlos.

### `The listen session expired. Opening a new one.`

**Causa:** la sesión de `listen` estuvo sin conexión con la API (corte de red, equipo suspendido). `listen` se
recupera solo, pero los eventos de ese intervalo no llegan.
**Remedio:** búscalos con `fiscalapi events list --from <fecha>` y reenvíalos con `fiscalapi events resend <eventId>`.

### `events resend` sin `--webhook` falla

```text
Error: Sorry, it's not me, it's you.
  Details: La escucha de CLI no está conectada; ejecuta fiscalapi listen.
  HTTP status: 409
  traceIdentifier (for support): <traceIdentifier>
```

**Causa:** sin `--webhook`, el reenvío va a tu sesión de `listen`, y no hay una abierta.
**Remedio:** abre `fiscalapi listen` en otra terminal o usa `--webhook <id>`.

## `fiscalapi trigger`

### 403 `fiscalapi trigger no está disponible en producción`

```text
$ fiscalapi trigger invoice.canceled --live
Error: Sorry, it's not me, it's you.
  Details: fiscalapi trigger no está disponible en producción; usa el ambiente de pruebas.
  HTTP status: 403
  traceIdentifier (for support): <traceIdentifier>
```

**Causa:** los eventos de prueba solo existen en el ambiente de pruebas: en producción se confundirían con uno real.
**Remedio:** quita `--live` y usa tus credenciales de pruebas.

### 409 `La escucha de CLI no está conectada; ejecuta fiscalapi listen.`

**Causa:** sin `--webhook`, el evento va a tu sesión de `listen`, y no hay una abierta con tu API key.
**Remedio:** abre `fiscalapi listen` en otra terminal y repite el `trigger`, o usa `--webhook <id>`.

### 400 `El tipo de evento '<tipo>' no existe.`

**Causa:** un tipo mal escrito (`invoice.cancelled` en lugar de `invoice.canceled`) o `webhook.test`, que no se
dispara con `trigger`. **Remedio:** usa uno de los tipos de `fiscalapi trigger --help`.

### El evento no llega a tu servidor

**Causa:** `trigger` respondió `Event triggered`, pero la entrega es de un solo intento: si tu servidor no respondió
2xx, queda fallida y no se reintenta.
**Remedio:** revisa la línea de `listen` (`[500]`, `[error: Timeout]`) o el historial de entregas del webhook en el
dashboard, corrige y repite con `fiscalapi events resend <eventId>`, con el mismo `--webhook` del `trigger` (sin él,
el reenvío va a tu sesión de `listen`).

## Instalación

### macOS bloquea el binario (Gatekeeper)

**Causa:** el binario no está firmado ni notarizado por Apple y lo bajaste con el navegador, que le pone la marca de
cuarentena. **Remedio:** `xattr -d com.apple.quarantine /usr/local/bin/fiscalapi`, o descárgalo con `curl`.

### Windows advierte al abrirlo (SmartScreen)

**Causa:** el binario no lleva firma Authenticode y el zip vino del navegador ("Windows protegió su PC").
**Remedio:** quita la marca de descarga con `Unblock-File` ([Instalación](instalacion.md#windows)). Si quieres
confirmar que el archivo está íntegro, [verifica la descarga](instalacion.md#verificar-la-descarga-opcional).

### `fiscalapi` no se encuentra después de instalar

**Causa:** la carpeta del binario no está en el `PATH`, o la terminal se abrió antes del cambio.
**Remedio:** abre una terminal nueva; en Windows revisa
[Agregar `fiscalapi` al PATH](instalacion.md#agregar-fiscalapi-al-path).

### Linux: `GLIBC_2.xx not found`

**Causa:** la distribución trae una glibc anterior a la 2.28, o usa musl (Alpine).
**Remedio:** usa una distribución con glibc 2.28 o más nueva, como RHEL 8+, Debian 10+ o Ubuntu 20.04+.

## Soporte

Si el problema sigue, abre un ticket con el comando, la salida completa con `--verbose` y el `traceIdentifier` del
error. Ninguno de los dos incluye tu API key ni tu tenant key.
