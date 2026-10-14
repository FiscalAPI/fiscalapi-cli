# Autenticación y ambientes

Cada comando que llama a la API manda tu **API key** (header `X-API-KEY`) y tu **tenant key** (`X-TENANT-KEY`). El CLI
las toma de las variables de entorno o, si no están, de las que guardó `fiscalapi login`. Ningún comando las acepta
como argumento: quedarían en el historial del shell y en la lista de procesos.

## Ambientes

| Opción | URL base | Uso |
|---|---|---|
| (ninguna) | `https://test.fiscalapi.com` | Pruebas. Es el ambiente por omisión. |
| `--live` | `https://live.fiscalapi.com` | Producción. |
| `--api-base <url>` | La que indiques | Otra URL base, p. ej. un back local. Gana sobre `--live`. |

`--api-base` acepta solo URLs absolutas `http` o `https`. Como las llaves viajan en headers, `http` solo se permite hacia
tu propio equipo (`localhost`, `127.0.0.1` o `::1`):

```text
$ fiscalapi webhooks list --api-base http://fiscalapi.example.com
Error: --api-base con http solo se permite para localhost, 127.0.0.1 o ::1; usa https para 'http://fiscalapi.example.com'.
Usa `fiscalapi --help` para ver los comandos y opciones.
```

Las credenciales se guardan **por ambiente**, una entrada por host (y puerto, si no es el de omisión): las de
`test.fiscalapi.com`, `live.fiscalapi.com` y `localhost:5001` son independientes.

## `fiscalapi login`

```text
$ fiscalapi login
API key: 
Tenant key: 
Listo. Credenciales guardadas para test.fiscalapi.com.
```

`login` pide la API key y la tenant key sin mostrar lo que escribes, las valida con `GET /api/v4/me/features` y las
guarda en el almacén de credenciales del sistema. Para producción, usa `fiscalapi login --live`. Los prompts y los
mensajes van a stderr.

| Caso | Resultado |
|---|---|
| La API rechaza las llaves (401) | No guarda nada y sale con 1: `Error: Las credenciales no son válidas para test.fiscalapi.com.` |
| Otro error de la API | No guarda nada y muestra el error de la API (salida 1). |
| La persona de la API key no tiene `Webhooks.Manage` | Guarda las llaves y avisa: `Esta API key no tiene el permiso Webhooks.Manage; listen, trigger y webhooks responderán 403.` |
| Llave vacía, de más de 200 caracteres o con caracteres que no son ASCII visible | Error de uso (salida 2): `La API key está vacía, es demasiado larga o tiene caracteres no válidos.` |

Los espacios alrededor de cada llave se ignoran.

### Sin terminal: stdin

Si stdin viene redirigido (un script), `login` no muestra prompts y lee **una llave por línea**: primero la API key y
luego la tenant key.

```bash
printf '%s\n%s\n' "$API_KEY" "$TENANT_KEY" | fiscalapi login
```

`printf` es un comando interno del shell, así que las llaves no aparecen en la lista de procesos. Un archivo guardado
como "UTF-8 con BOM" también funciona.

> **Terminales sin ConPTY.** En mintty (la terminal de Git Bash en Windows) y en `docker run -i` sin `-t`, la entrada
> no llega como terminal: `login` la trata como redirigida, sin prompt y **mostrando lo que escribes**. Usa Windows
> Terminal o PowerShell, `docker run -it`, o las variables de entorno.

## `fiscalapi logout`

Borra la entrada del ambiente. Termina con 0 aunque no hubiera nada guardado:

```text
$ fiscalapi logout
Credenciales borradas para test.fiscalapi.com.
$ fiscalapi logout --live
No había credenciales guardadas para live.fiscalapi.com.
```

Si el almacén está bloqueado o falla, la entrada puede seguir ahí: `logout` lo dice y sale con 1.

`logout` es local: no llama a la API ni revoca la API key. Para revocarla, usa el dashboard de FiscalAPI.

## Dónde se guardan

| Sistema | Almacén | Entrada |
|---|---|---|
| Windows | Administrador de credenciales (Credential Manager), "Credenciales de Windows" | Credencial genérica `fiscalapi-cli:<host>` |
| macOS | Llavero de sesión ("Acceso a Llaveros") | Contraseña genérica con servicio `fiscalapi-cli`, cuenta `<host>` y nombre `FiscalAPI CLI (<host>)` |
| Linux | Secret Service por libsecret (GNOME Keyring o compatible) | Secreto `FiscalAPI CLI (<host>)` con los atributos `service=fiscalapi-cli` y `host=<host>` |

No hay respaldo en archivo: si el sistema no tiene almacén, `login` no guarda nada en texto plano.

## Variables de entorno

| Variable | Valor |
|---|---|
| `FISCALAPI_API_KEY` | Tu API key. |
| `FISCALAPI_TENANT_KEY` | Tu tenant key. |

Si las dos están definidas, ganan sobre lo que guardó `login` y valen para **cualquier** ambiente: con llaves de
pruebas, no agregues `--live` o la API responderá 401. Una variable vacía cuenta como no definida.

Se definen **juntas**. Con una sola, el CLI no la completa con lo guardado y sale con 2:

```text
Error: Define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY juntas; solo está definida FISCALAPI_API_KEY.
```

Sin variables y sin nada guardado para el ambiente, también sale con 2:

```text
Error: No hay credenciales para test.fiscalapi.com. Ejecuta `fiscalapi login` o define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.
```

## Casos especiales

### CI

En un CI no hay almacén ni nadie que escriba: define las dos variables como secretos del CI y no uses `login`.

```yaml
# GitHub Actions
env:
  FISCALAPI_API_KEY: ${{ secrets.FISCALAPI_API_KEY }}
  FISCALAPI_TENANT_KEY: ${{ secrets.FISCALAPI_TENANT_KEY }}
```

Usa `--json` para procesar la salida y revisa el código de salida ([Salida y códigos](salida-y-codigos.md)). Con `CI`
definida, el aviso de versión nueva no corre ([Instalación](instalacion.md#aviso-de-versión-nueva)).

### WSL, servidores y contenedores

En Linux sin Secret Service (WSL, un servidor sin sesión gráfica, un contenedor), `login` sale con 2 antes de pedir
las llaves:

```text
Error: No hay un almacén de credenciales disponible en este sistema. Define FISCALAPI_API_KEY y FISCALAPI_TENANT_KEY.
  Detalle: No se encontró libsecret-1.so.0 (paquete libsecret-1-0 o libsecret).
```

El `Detalle` cambia según la causa: falta libsecret o no hay Secret Service en la sesión de D-Bus. Usa las variables
de entorno. Si quieres el almacén, instala libsecret (`libsecret-1-0` en Debian/Ubuntu, `libsecret` en RHEL/Fedora) y
un servicio como GNOME Keyring dentro de una sesión de D-Bus.

### Almacén bloqueado

Si el almacén existe pero está bloqueado o no respondió (un llavero bloqueado, GNOME Keyring sin desbloquear), el
comando sale con 1:

```text
Error: El almacén de credenciales del sistema existe, pero está bloqueado o no respondió.
  Detalle: …
```

Desbloquéalo (en macOS, abre "Acceso a Llaveros"; en Linux, inicia sesión en el escritorio o desbloquea el llavero) y
repite el comando, o usa las variables de entorno.

### Entrada guardada ilegible

Si la entrada guardada se dañó, el comando sale con 1:

```text
Error: La entrada guardada para test.fiscalapi.com no se puede leer. Ejecuta `fiscalapi logout` y luego `fiscalapi login`.
```

Siguiente: [`fiscalapi listen`](listen.md).
