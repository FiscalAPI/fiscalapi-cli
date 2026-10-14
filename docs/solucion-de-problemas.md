# Solución de problemas

Cada caso trae el texto que muestra el CLI o la API, la causa y el remedio. Para más detalle de una llamada a la API,
repite el comando con `--verbose` ([Salida y códigos](salida-y-codigos.md#--verbose)); nunca muestra tus llaves.

## Credenciales

### 401 `Invalid tenant key`

```text
Error: Sorry, it's not me, it's you.
  Detalle: Invalid tenant key
  Código HTTP: 401
  traceIdentifier (para soporte): …
  Sugerencia: Ejecuta `fiscalapi login` o revisa las variables de entorno FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.
```

**Causa:** la tenant key no existe en ese ambiente, o la API key y la tenant key son de ambientes distintos (las de
pruebas no sirven con `--live` y al revés).
**Remedio:** revisa la tenant key en el dashboard del ambiente correcto y vuelve a correr `fiscalapi login` (o
`fiscalapi login --live`). Si usas variables de entorno, recuerda que ganan sobre lo guardado.

### 401 `Missing required http X-API-KEY.`

**Causa:** la API key no es válida para ese ambiente (revocada, mal copiada o de otro ambiente).
**Remedio:** el mismo de arriba. `fiscalapi login` valida las llaves antes de guardarlas:
`Error: Las credenciales no son válidas para test.fiscalapi.com.`

### 403 sin `Webhooks.Manage`

```text
Error: Sorry, it's not me, it's you.
  Detalle: The authenticated credentials do not have permission to perform this operation.
  Código HTTP: 403
```

**Causa:** la persona de la API key no tiene el permiso `Webhooks.Manage`, que piden `listen`, `trigger`, `webhooks` y
`events`. `login` ya lo había advertido:
`Esta API key no tiene el permiso Webhooks.Manage; listen, trigger y webhooks responderán 403.`
**Remedio:** pide al owner de la cuenta que dé el permiso a esa persona, o usa una API key de una persona que lo tenga.

### `No hay credenciales para …`

```text
Error: No hay credenciales para test.fiscalapi.com. Ejecuta `fiscalapi login` o define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.
```

**Causa:** no hay variables de entorno ni nada guardado para **ese** ambiente. Las credenciales se guardan por host:
haber hecho `login` en pruebas no sirve para `--live` ni para otro `--api-base`.
**Remedio:** `fiscalapi login` con las mismas opciones de ambiente que usa el comando.

### `Define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY juntas`

**Causa:** solo una de las dos variables está definida. **Remedio:** define las dos o ninguna.

### No hay almacén de credenciales

```text
Error: No hay un almacén de credenciales disponible en este sistema. Define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.
  Detalle: …
```

**Causa:** Linux sin Secret Service: WSL, un servidor sin sesión gráfica, un contenedor o falta libsecret.
**Remedio:** usa las variables de entorno, o instala libsecret y GNOME Keyring
([Autenticación](autenticacion.md#wsl-servidores-y-contenedores)).

### Almacén bloqueado

```text
Error: El almacén de credenciales del sistema existe, pero está bloqueado o no respondió.
```

**Causa:** el llavero de macOS o GNOME Keyring está bloqueado, o no respondió a tiempo.
**Remedio:** desbloquéalo y repite el comando, o usa las variables de entorno.

### `login` muestra lo que escribo

**Causa:** la terminal no tiene ConPTY (mintty de Git Bash) o es `docker run -i` sin `-t`; `login` trata la entrada
como redirigida, sin prompt y con eco.
**Remedio:** usa Windows Terminal o PowerShell, `docker run -it`, o las variables de entorno.

## Opciones y URLs

### `--api-base con http solo se permite para localhost…`

```text
Error: --api-base con http solo se permite para localhost, 127.0.0.1 o ::1; usa https para 'http://fiscalapi.example.com'.
```

**Causa:** tus llaves viajan en headers; sin TLS solo se permite hablar con tu propio equipo.
**Remedio:** usa `https://`. Para un back local, `http://localhost:<puerto>` sí está permitido.

### Opciones que no se combinan

Son errores de uso (salida 2) y dicen qué corregir, por ejemplo:

```text
Error: --skip-verify solo aplica con --forward-to.
Error: --print-secret no se combina con --forward-to.
Error: --person necesita al menos un id de persona; para recibir los eventos de todas usa --all-persons.
Error: Para borrar sin confirmación interactiva usa --yes.
```

`fiscalapi <comando> --help` lista las opciones de cada comando.

## `fiscalapi listen`

### 409 `Otra sesión de fiscalapi listen reemplazó a esta.`

**Causa:** abriste otro `fiscalapi listen` o un `fiscalapi listen --print-secret` con la misma persona (en otra
terminal, otra máquina o un script). Solo puede haber una sesión por persona; la anterior termina con código 1.
**Remedio:** cierra la otra y vuelve a abrir `listen`. Corre `--print-secret` antes de `listen`, no mientras corre.

### El receptor no responde: `[error: Timeout]`, `[error: ConnectionFailed]`

```text
2026-10-12 09:47:46  --> invoice.canceled [evt_0001] [error: ConnectionFailed]
```

**Causa:** nada escucha en la URL de `--forward-to` (puerto equivocado, servidor apagado, ruta en otro host), o tu
servidor tardó más de 15 s (`Timeout`). Dentro de un contenedor, `localhost` es el propio contenedor.
**Remedio:** prueba la URL con `curl -X POST <url>`; responde 2xx rápido y procesa después. Con un certificado
autofirmado verás `[error: TlsFailed]`: agrega `--skip-verify`. La entrega no se reintenta; pídela otra vez con
`fiscalapi events resend <eventId>`.

### La firma no verifica en mi servidor

**Causa más común:** tu servidor verifica un cuerpo **re-serializado**. Si el framework deserializa el JSON (por ejemplo
`[FromBody]` en ASP.NET Core o `express.json()` en Express) y lo vuelves a serializar, cambian los espacios, el orden o
los escapes, y la firma ya no coincide. `listen` reenvía los bytes exactos que firmó FiscalAPI.
**Remedio:** lee el cuerpo crudo antes de deserializarlo y pásalo al `Verify` del SDK
([listen](listen.md#verificar-la-firma-en-tu-servidor-local)).

Otras causas:

- **Otro secreto:** el de tu escucha de CLI no es el de tus webhooks con URL. Toma el de la línea
  `Listo. Tu secreto de firma es whsec_…` o de `fiscalapi listen --print-secret`, completo y con su prefijo `whsec_`.
- **Reloj desfasado:** los SDK rechazan un `webhook-timestamp` con más de 5 minutos de diferencia. Sincroniza la hora
  del equipo.
- **Rotaste el secreto:** durante 24 horas configura los dos secretos (los SDK aceptan una lista).
- **Headers alterados:** un proxy local que cambia `webhook-id`, `webhook-timestamp` o `webhook-signature`. `--headers`
  no puede cambiarlos.

### `La sesión de fiscalapi listen caducó; se abre una nueva.`

**Causa:** pasaron más de 60 s sin long-polling (corte de red, suspensión del equipo). `listen` se recupera solo, pero
los eventos de ese intervalo no llegan. **Remedio:** búscalos con `fiscalapi events list --from <fecha>` y reenvíalos
con `fiscalapi events resend <eventId>`.

### `events resend` sin `--webhook` falla

```text
Error: Sorry, it's not me, it's you.
  Detalle: La escucha de CLI no está conectada; ejecuta fiscalapi listen.
```

**Causa:** sin `--webhook`, el reenvío va a tu escucha de CLI, y no hay un `listen` abierto. **Remedio:** abre
`fiscalapi listen` en otra terminal o usa `--webhook <id>`.

## Instalación

### macOS bloquea el binario (Gatekeeper)

**Causa:** el binario no está firmado ni notarizado por Apple y lo bajaste con el navegador, que le pone la marca de
cuarentena. **Remedio:** `xattr -d com.apple.quarantine /usr/local/bin/fiscalapi`, o descárgalo con `curl`.

### Windows advierte al abrirlo (SmartScreen)

**Causa:** el binario no lleva firma Authenticode y el zip vino del navegador ("Windows protegió su PC").
**Remedio:** verifica el SHA-256 ([Instalación](instalacion.md#windows-powershell)) y quita la marca de descarga con
`Unblock-File`.

### `fiscalapi` no se encuentra después de instalar

**Causa:** la carpeta del binario no está en el `PATH`, o la terminal se abrió antes del cambio.
**Remedio:** abre una terminal nueva; en Windows revisa
[Agregar `fiscalapi` al PATH](instalacion.md#agregar-fiscalapi-al-path).

### Linux: `GLIBC_2.xx not found`

**Causa:** la distro es más vieja que glibc 2.28 (o es Alpine, con musl). **Remedio:** usa RHEL/AlmaLinux/Rocky 8+,
Debian 10+ o Ubuntu 20.04+.

## Soporte

Si el problema sigue, abre un ticket con el comando, la salida completa con `--verbose` y el `traceIdentifier` del
error. Ninguna de las dos incluye tus llaves.
