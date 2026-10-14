# Autenticación y ambientes

Cada comando que llama a la API manda tu **API key** (header `X-API-KEY`) y tu **tenant key** (`X-TENANT-KEY`). El CLI
las toma de las variables de entorno o, si no están, de las que guardó `fiscalapi login`. Nunca se aceptan como
argumento de un comando, porque un argumento queda en el historial del shell y en la lista de procesos.

## Ambientes

| Opción | URL base | Uso |
|---|---|---|
| (ninguna) | `https://test.fiscalapi.com` | Pruebas. Es el ambiente por omisión. |
| `--live` | `https://live.fiscalapi.com` | Producción. |
| `--api-base <url>` | La que indiques | Otra URL base, p. ej. un back local. Gana sobre `--live`. |

`--api-base` acepta solo URLs absolutas `http` o `https`, y `http` solo hacia el propio equipo (`localhost`,
`127.0.0.1` o `::1`), porque las llaves viajan en headers:

```text
$ fiscalapi webhooks list --api-base http://fiscalapi.example.com
Error: --api-base con http solo se permite para localhost, 127.0.0.1 o ::1; usa https para 'http://fiscalapi.example.com'.
Usa `fiscalapi --help` para ver los comandos y opciones.
```

Las credenciales se guardan **por ambiente**: una entrada por host (y puerto, si no es el de omisión). Las de
`test.fiscalapi.com`, las de `live.fiscalapi.com` y las de `localhost:5001` son independientes.

## `fiscalapi login`

```text
$ fiscalapi login
API key: 
Tenant key: 
Listo. Credenciales guardadas para test.fiscalapi.com.
```

`login`:

1. pide la API key y la tenant key sin mostrar lo que escribes;
2. las valida contra la API con `GET /api/v4/me/features`;
3. las guarda en el almacén de credenciales del sistema, en la entrada del ambiente.

Para producción usa `fiscalapi login --live`. Los prompts y los mensajes van a stderr.

Si las llaves no son válidas para ese ambiente, no se guarda nada y sale con 1:

```text
Error: Las credenciales no son válidas para test.fiscalapi.com.
```

Si la API key es válida pero su persona no tiene el permiso `Webhooks.Manage`, `login` las guarda y avisa:

```text
Esta API key no tiene el permiso Webhooks.Manage; listen, trigger y webhooks responderán 403.
```

Cada llave admite hasta 200 caracteres ASCII visibles; los espacios alrededor se ignoran. Una llave vacía o con otros
caracteres es un error de uso (salida 2): `La API key está vacía, es demasiado larga o tiene caracteres no válidos.`

### Sin terminal: stdin

Si stdin viene redirigido (un script), `login` no muestra prompts y lee **una llave por línea**: primero la API key y
luego la tenant key.

```bash
printf '%s\n%s\n' "$API_KEY" "$TENANT_KEY" | fiscalapi login
```

`printf` es un comando interno del shell, así que las llaves no aparecen en la lista de procesos. Un archivo guardado
como "UTF-8 con BOM" también funciona.

> **Terminales sin ConPTY.** En mintty (la terminal de Git Bash en Windows) y en `docker run -i` sin `-t`, la entrada
> no llega como una terminal: `login` la trata como redirigida, sin prompt y **mostrando lo que escribes**. Usa Windows
> Terminal o PowerShell, `docker run -it`, o las variables de entorno.

## `fiscalapi logout`

Borra la entrada del ambiente y siempre termina con 0, aunque no hubiera nada guardado:

```text
$ fiscalapi logout
Credenciales borradas para test.fiscalapi.com.
$ fiscalapi logout --live
No había credenciales guardadas para live.fiscalapi.com.
```

`logout` es local: no llama a la API ni revoca la API key. Para revocarla, hazlo desde el dashboard de FiscalAPI.

## Dónde se guardan

| Sistema | Almacén | Entrada |
|---|---|---|
| Windows | Administrador de credenciales (Credential Manager), "Credenciales de Windows" | Credencial genérica `fiscalapi-cli:<host>` |
| macOS | Llavero de sesión ("Acceso a Llaveros") | Contraseña genérica con servicio `fiscalapi-cli`, cuenta `<host>` y nombre `FiscalAPI CLI (<host>)` |
| Linux | Secret Service por libsecret (GNOME Keyring o compatible) | Secreto `FiscalAPI CLI (<host>)` con los atributos `service=fiscalapi-cli` y `host=<host>` |

No hay respaldo en un archivo: si el sistema no tiene almacén, `login` no guarda nada en texto plano.

## Variables de entorno

| Variable | Valor |
|---|---|
| `FISCALAPI_API_KEY` | Tu API key. |
| `FISCALAPI_TENANT_KEY` | Tu tenant key. |

**Precedencia:** si las dos están definidas, ganan sobre lo que guardó `login`, para cualquier ambiente. Se definen
**juntas**; con una sola, el CLI no adivina y sale con 2:

```text
Error: Define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY juntas; solo está definida FISCALAPI_API_KEY.
```

Sin variables y sin nada guardado para el ambiente:

```text
Error: No hay credenciales para test.fiscalapi.com. Ejecuta `fiscalapi login` o define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.
```

Las variables valen para **cualquier** ambiente al que apunte el comando. Si usas llaves de pruebas en las variables,
no agregues `--live`: la API las rechazaría con 401.

## Casos especiales

### CI

En un CI no hay almacén ni persona que escriba: define las dos variables como secretos del CI y no uses `login`.

```yaml
# GitHub Actions
env:
  FISCALAPI_API_KEY: ${{ secrets.FISCALAPI_API_KEY }}
  FISCALAPI_TENANT_KEY: ${{ secrets.FISCALAPI_TENANT_KEY }}
```

Usa `--json` para procesar la salida y revisa el código de salida ([Salida y códigos](salida-y-codigos.md)).
El aviso de versión nueva no corre con `CI=true`.

### WSL, servidores y contenedores

En Linux sin Secret Service (WSL, un servidor sin sesión gráfica, un contenedor) `login` sale con 2 antes de pedir
las llaves:

```text
Error: No hay un almacén de credenciales disponible en este sistema. Define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.
  Detalle: No se encontró libsecret-1.so.0 (paquete libsecret-1-0 o libsecret).
```

El `Detalle` cambia según la causa (falta libsecret o no hay Secret Service en la sesión de D-Bus). Define las
variables de entorno. Si quieres el almacén, instala libsecret (`libsecret-1-0` en Debian/Ubuntu, `libsecret`
en RHEL/Fedora) y un servicio como GNOME Keyring dentro de una sesión de D-Bus.

### Almacén bloqueado

Si el almacén existe pero está bloqueado o no respondió (un llavero bloqueado, GNOME Keyring sin desbloquear), el
comando sale con 1:

```text
Error: El almacén de credenciales del sistema existe, pero está bloqueado o no respondió.
  Detalle: …
```

Desbloquéalo (en macOS, abre "Acceso a Llaveros"; en Linux, inicia sesión en el escritorio o desbloquea el llavero) y
vuelve a intentarlo, o usa las variables de entorno.

### Entrada guardada ilegible

Si la entrada guardada se dañó:

```text
Error: La entrada guardada para test.fiscalapi.com no se puede leer. Ejecuta `fiscalapi logout` y luego `fiscalapi login`.
```

Siguiente: [`fiscalapi listen`](listen.md).
