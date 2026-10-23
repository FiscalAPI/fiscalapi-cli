# Autenticación y ambientes

Cada comando que llama a la API manda tu API key (header `X-API-KEY`) y tu tenant key (`X-TENANT-KEY`). El CLI las
toma de las variables de entorno o, si no están, de las que guardó `fiscalapi login`. Ningún comando las acepta como
argumento: quedarían en el historial del shell y en la lista de procesos.

## Ambientes

| Opción             | URL base                     | Uso                                                            |
| ------------------ | ---------------------------- | -------------------------------------------------------------- |
| (ninguna)          | `https://test.fiscalapi.com` | Pruebas. Es el ambiente por omisión.                           |
| `--live`           | `https://live.fiscalapi.com` | Producción.                                                    |
| `--api-base <url>` | La que indiques              | Otra URL base, por ejemplo una API local. Gana sobre `--live`. |

`--api-base` acepta solo URLs absolutas `http` o `https`. Como la API key y la tenant key viajan en headers, `http` solo
se permite hacia tu propio equipo (`localhost`, `127.0.0.1` o `::1`):

```text
$ fiscalapi webhooks list --api-base http://fiscalapi.example.com
Error: --api-base allows http only for localhost, 127.0.0.1 or ::1. Use https for 'http://fiscalapi.example.com'.
Run `fiscalapi --help` to see commands and options.
```

Las credenciales se guardan por ambiente, una entrada por host (y puerto, si no es el de omisión): las de
`test.fiscalapi.com`, `live.fiscalapi.com` y `localhost:<puerto>` son independientes.

## `fiscalapi login`

```text
$ fiscalapi login
API key:
Tenant key:
Done. Credentials saved for test.fiscalapi.com.
```

`login` pide la API key y la tenant key sin mostrar lo que escribes, las valida con la API y las guarda en el almacén
de credenciales del sistema. Para producción, usa `fiscalapi login --live`. Los prompts y los mensajes van a stderr.
Los espacios alrededor de cada valor se ignoran.

| Caso                                                                            | Resultado                                                                                                                           |
| ------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| La API rechaza la API key o la tenant key (401)                                 | No guarda nada y sale con 1: `Error: Invalid credentials for test.fiscalapi.com.`                                                   |
| Otro error de la API                                                            | No guarda nada, muestra el error de la API y sale con 1.                                                                            |
| La persona de la API key no tiene el permiso `Webhooks.Manage`                  | Guarda las credenciales y avisa: `This API key lacks the Webhooks.Manage permission. listen, trigger and webhooks will return 403.` |
| Valor vacío, de más de 200 caracteres o con caracteres que no son ASCII visible | Sale con 2: `Error: The API key is empty, too long, or has invalid characters.`                                                     |

### Sin terminal: stdin

Si stdin viene redirigido (un script), `login` no muestra prompts y lee un valor por línea: primero la API key y luego
la tenant key.

```bash
printf '%s\n%s\n' "$API_KEY" "$TENANT_KEY" | fiscalapi login
```

`printf` es un comando interno del shell, así que los valores no aparecen en la lista de procesos. Un archivo guardado
como "UTF-8 con BOM" también funciona.

> **Terminales sin TTY.** En mintty (la terminal de Git Bash en Windows) y en `docker run -i` sin `-t`, la entrada no
> llega como terminal: `login` la trata como redirigida, sin prompt y mostrando lo que escribes. Usa Windows Terminal o
> PowerShell, `docker run -it`, o las variables de entorno.

## `fiscalapi logout`

Elimina las credenciales guardadas del ambiente. Termina con 0 aunque no hubiera nada guardado:

```text
$ fiscalapi logout
Credentials deleted for test.fiscalapi.com.
$ fiscalapi logout --live
No saved credentials for live.fiscalapi.com.
```

Si el almacén está bloqueado o falla, la entrada puede seguir ahí: `logout` lo dice y sale con 1.

`logout` es local: no llama a la API ni revoca la API key. Para revocarla, usa el dashboard de FiscalAPI.

## Dónde se guardan

| Sistema | Almacén                                                  | Entrada                                                                                             |
| ------- | -------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| Windows | Administrador de credenciales, "Credenciales de Windows" | Credencial genérica `fiscalapi-cli:<host>`                                                          |
| macOS   | Llavero de sesión ("Acceso a Llaveros")                  | Contraseña genérica con servicio `fiscalapi-cli`, cuenta `<host>` y nombre `FiscalAPI CLI (<host>)` |
| Linux   | Secret Service (GNOME Keyring o compatible)              | Entrada `FiscalAPI CLI (<host>)` con los atributos `service=fiscalapi-cli` y `host=<host>`          |

No hay respaldo en archivo: si el sistema no tiene almacén, `login` no guarda nada en texto plano.

## Variables de entorno

| Variable               | Valor          |
| ---------------------- | -------------- |
| `FISCALAPI_API_KEY`    | Tu API key.    |
| `FISCALAPI_TENANT_KEY` | Tu tenant key. |

Si las dos están definidas, ganan sobre lo que guardó `login` y valen para cualquier ambiente: con una API key de
pruebas, no agregues `--live` o la API responderá 401. Una variable vacía cuenta como no definida.

Se definen juntas. Con una sola, el CLI no la completa con lo guardado y sale con 2:

```text
Error: Set both FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY; only FISCALAPI_API_KEY is set.
```

Sin variables y sin nada guardado para el ambiente, también sale con 2:

```text
Error: No credentials for test.fiscalapi.com. Run `fiscalapi login` or set FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY.
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
definida, el aviso de versión nueva no aparece ([Instalación](instalacion.md#aviso-de-versión-nueva)).

### WSL, servidores y contenedores

En Linux sin Secret Service (WSL, un servidor sin sesión gráfica, un contenedor), `login` sale con 2 antes de pedir la
API key:

```text
Error: No credential store is available. Set FISCALAPI_API_KEY and FISCALAPI_TENANT_KEY.
  Details: <motivo>
```

La línea `Details` dice qué falta. Usa las variables de entorno o, si quieres el almacén, instala GNOME Keyring (o un
servicio compatible con Secret Service) y abre una sesión de escritorio.

### Almacén bloqueado

Si el almacén existe pero está bloqueado o no respondió, el comando sale con 1:

```text
Error: The credential store is locked or not responding. Unlock it and try again.
  Details: <motivo>
```

Desbloquéalo (en macOS, abre "Acceso a Llaveros"; en Linux, inicia sesión en el escritorio o desbloquea el llavero) y
repite el comando, o usa las variables de entorno.

### Entrada guardada ilegible

Si la entrada guardada se dañó, el comando sale con 1:

```text
Error: Saved credentials for test.fiscalapi.com are unreadable. Run `fiscalapi logout`, then `fiscalapi login`.
```

Siguiente: [`fiscalapi listen`](listen.md).
