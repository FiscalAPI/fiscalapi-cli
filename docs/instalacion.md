# Instalación

`fiscalapi` es un solo binario. Descárgalo, ponlo en una carpeta de tu `PATH` y listo: no requiere instalar nada más.

## Sistemas y empaques

Cada [release](https://github.com/FiscalAPI/fiscalapi-cli/releases) trae un empaque por sistema, con solo el binario,
y un archivo `SHA256SUMS` con el SHA-256 de cada empaque.

| Sistema             | Empaque                      | Requisito                                                                |
| ------------------- | ---------------------------- | ------------------------------------------------------------------------ |
| Windows x64         | `fiscalapi-win-x64.zip`      | Windows 10 u 11, o Windows Server 2012 o más nuevo                       |
| Linux x64           | `fiscalapi-linux-x64.tar.gz` | Linux x64 con glibc 2.28 o más nueva: RHEL 8+, Debian 10+, Ubuntu 20.04+ |
| macOS Apple Silicon | `fiscalapi-osx-arm64.tar.gz` | macOS 15 o más nuevo, Mac con chip Apple (M1 o posterior)                |
| macOS Intel         | `fiscalapi-osx-x64.tar.gz`   | macOS 15 o más nuevo, Mac con procesador Intel                           |

El binario de Linux no corre en distribuciones con musl, como Alpine.

Los comandos de abajo descargan la última versión publicada. Para una versión fija, cambia `releases/latest/download`
por `releases/download/v<versión>`, por ejemplo `releases/download/v4.0.462`.

## Linux

```bash
curl -fsSLO https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download/fiscalapi-linux-x64.tar.gz
tar -xzf fiscalapi-linux-x64.tar.gz
sudo install -m 0755 fiscalapi /usr/local/bin/fiscalapi
fiscalapi --version
```

Sin permisos de administrador, instálalo en tu usuario con `install -D -m 0755 fiscalapi ~/.local/bin/fiscalapi` y
revisa que `~/.local/bin` esté en tu `PATH`.

## macOS

Usa `fiscalapi-osx-arm64.tar.gz` en una Mac con Apple Silicon y `fiscalapi-osx-x64.tar.gz` en una Intel (`uname -m`
dice `arm64` o `x86_64`).

```bash
package=fiscalapi-osx-arm64.tar.gz
curl -fsSLO "https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download/$package"
tar -xzf "$package"
sudo mkdir -p /usr/local/bin
sudo install -m 0755 fiscalapi /usr/local/bin/fiscalapi
fiscalapi --version
```

El binario no está firmado ni notarizado por Apple. Descargado con `curl` corre sin más; si lo bajaste con el
navegador, Gatekeeper lo bloquea la primera vez. Quítale la marca de cuarentena:

```bash
xattr -d com.apple.quarantine /usr/local/bin/fiscalapi
```

## Windows

En PowerShell:

```powershell
$base = 'https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download'
Invoke-WebRequest "$base/fiscalapi-win-x64.zip" -OutFile fiscalapi-win-x64.zip
Expand-Archive fiscalapi-win-x64.zip -DestinationPath (Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi') -Force
```

El binario no lleva firma Authenticode. Si bajaste el zip con el navegador, SmartScreen puede advertir al abrirlo
("Windows protegió su PC"). Quita la marca de descarga:

```powershell
Unblock-File (Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi\fiscalapi.exe')
```

### Agregar `fiscalapi` al PATH

Este bloque agrega la carpeta al `Path` de tu usuario, sin alterar las rutas que ya tiene y sin duplicarla:

```powershell
$dir = Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi'
$environment = Get-Item 'HKCU:\Environment'
$userPath = $environment.GetValue('Path', '', 'DoNotExpandEnvironmentNames')
if (($userPath -split ';') -notcontains $dir) {
    $newPath = (@($userPath.TrimEnd(';'), $dir) | Where-Object { $_ }) -join ';'
    Set-ItemProperty -Path 'HKCU:\Environment' -Name Path -Value $newPath -Type ExpandString
    [Environment]::SetEnvironmentVariable('FISCALAPI_CLI_PATH_REFRESH', [NullString]::Value, 'User')
}
```

La última línea avisa a Windows del cambio para que las terminales nuevas lo vean; no crea ninguna variable.

A mano: busca "Editar las variables de entorno de esta cuenta" en el menú Inicio, abre `Path`, elige **Nuevo** y
escribe `%LOCALAPPDATA%\Programs\fiscalapi`.

Abre una terminal nueva y corre `fiscalapi --version`.

## Verificar la descarga (opcional)

Puedes comparar el SHA-256 del empaque con el del archivo `SHA256SUMS` del mismo release. Descarga `SHA256SUMS` en la
carpeta donde está el empaque:

```bash
curl -fsSLO https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download/SHA256SUMS
```

```powershell
Invoke-WebRequest 'https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download/SHA256SUMS' -OutFile SHA256SUMS
```

Luego compáralo. En Linux debe decir `fiscalapi-linux-x64.tar.gz: OK`:

```bash
grep ' fiscalapi-linux-x64.tar.gz$' SHA256SUMS | sha256sum -c -
```

En macOS, con el mismo `$package` de la instalación, debe decir `OK`:

```bash
grep " $package\$" SHA256SUMS | shasum -a 256 -c -
```

En Windows debe decir `True`:

```powershell
$expected = (Select-String -Path SHA256SUMS -Pattern ' fiscalapi-win-x64\.zip$').Line.Split(' ')[0]
(Get-FileHash fiscalapi-win-x64.zip -Algorithm SHA256).Hash -eq $expected
```

Si no coincide, elimina lo que descargaste y vuelve a bajarlo.

## Actualizar

Repite la instalación de tu sistema: el binario nuevo reemplaza al anterior. Tus credenciales de `fiscalapi login` se
conservan, porque viven en el almacén de credenciales del sistema y no junto al binario.

`fiscalapi --version` imprime la versión instalada:

```text
$ fiscalapi --version
4.0.462
```

### Aviso de versión nueva

Mientras corre un comando, `fiscalapi` consulta en GitHub el último release, como mucho una vez cada 24 horas y sin
enviar tus credenciales. Si hay uno más nuevo, al terminar el comando escribe en stderr:

```text

fiscalapi 4.0.463 is available (you have 4.0.462).
Download it from https://github.com/FiscalAPI/fiscalapi-cli/releases/tag/v<version>
```

- La consulta tarda como mucho 2 s. Si GitHub no responde, la respuesta no es válida o el archivo de estado no se puede
  escribir, no se muestra nada y el comando termina con su mismo código de salida.
- Solo se anuncian releases publicados y estables, nunca una prerelease (`4.1.0-rc.1`).
- Tras Ctrl+C, el aviso solo aparece si la respuesta ya había llegado.

El aviso no aparece en estos casos:

| Caso                                         | Por qué                             |
| -------------------------------------------- | ----------------------------------- |
| Con `--json`                                 | La salida es para otra herramienta. |
| Con stderr redirigido (`2> log.txt`)         | No ensucia el log de un script.     |
| Con la variable `CI` definida, como en un CI | En un CI no hay quién lo lea.       |
| Con `FISCALAPI_NO_UPDATE_CHECK=1`            | Para apagarlo siempre.              |

`CI` y `FISCALAPI_NO_UPDATE_CHECK` cuentan como definidas con cualquier valor salvo vacío, `0` o `false`. Para apagar
el aviso de forma permanente:

```bash
echo 'export FISCALAPI_NO_UPDATE_CHECK=1' >> ~/.bashrc    # o ~/.zshrc
```

```powershell
[Environment]::SetEnvironmentVariable('FISCALAPI_NO_UPDATE_CHECK', '1', 'User')
```

La fecha de la última consulta, sin ningún secreto, se guarda en:

| Sistema | Archivo                                                                              |
| ------- | ------------------------------------------------------------------------------------ |
| Windows | `%LOCALAPPDATA%\fiscalapi-cli\update-check.json`                                     |
| macOS   | `~/Library/Application Support/fiscalapi-cli/update-check.json`                      |
| Linux   | `$XDG_STATE_HOME/fiscalapi-cli/update-check.json` (por omisión, en `~/.local/state`) |

Eliminarlo solo adelanta la siguiente consulta. Si la carpeta no se puede escribir, el CLI no consulta.

## Desinstalar

1. Elimina las credenciales guardadas de cada ambiente que usaste, antes de eliminar el binario:

   ```bash
   fiscalapi logout
   fiscalapi logout --live
   fiscalapi logout --api-base http://localhost:<puerto>   # si usaste una API local
   ```

2. Elimina el binario y la carpeta de estado:

   ```bash
   # Linux
   sudo rm /usr/local/bin/fiscalapi
   rm -rf "${XDG_STATE_HOME:-$HOME/.local/state}/fiscalapi-cli"
   # macOS
   sudo rm /usr/local/bin/fiscalapi
   rm -rf ~/Library/Application\ Support/fiscalapi-cli
   ```

   En Windows, quita también la carpeta del `Path` de tu usuario, sin tocar el resto:

   ```powershell
   $dir = Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi'
   $environment = Get-Item 'HKCU:\Environment'
   $userPath = $environment.GetValue('Path', '', 'DoNotExpandEnvironmentNames')
   $newPath = (($userPath -split ';') | Where-Object { $_ -and $_ -ne $dir }) -join ';'
   if ($newPath -ne $userPath) {
       Set-ItemProperty -Path 'HKCU:\Environment' -Name Path -Value $newPath -Type ExpandString
       [Environment]::SetEnvironmentVariable('FISCALAPI_CLI_PATH_REFRESH', [NullString]::Value, 'User')
   }
   Remove-Item $dir -Recurse -Force
   Remove-Item (Join-Path $env:LOCALAPPDATA 'fiscalapi-cli') -Recurse -Force -ErrorAction SilentlyContinue
   ```

Siguiente: [Autenticación](autenticacion.md).
