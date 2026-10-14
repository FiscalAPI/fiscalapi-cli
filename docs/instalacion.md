# Instalación

`fiscalapi` es un solo binario nativo (.NET Native AOT): no necesitas instalar .NET ni ninguna otra dependencia.

## Sistemas y empaques

Cada [release](https://github.com/FiscalAPI/fiscalapi-cli/releases) trae un empaque por sistema y un archivo
`SHA256SUMS` con el SHA-256 de cada empaque. Cada empaque contiene solo el binario.

| Sistema | Empaque | Requisito |
|---|---|---|
| Windows x64 | `fiscalapi-win-x64.zip` | Windows 10 u 11, o Windows Server 2012 o más nuevo |
| Linux x64 | `fiscalapi-linux-x64.tar.gz` | glibc 2.28 o más nueva: RHEL/AlmaLinux/Rocky 8+, Debian 10+, Ubuntu 20.04+. No corre en Alpine (musl). |
| macOS Apple Silicon | `fiscalapi-osx-arm64.tar.gz` | macOS 15 o más nuevo, Mac con chip Apple (M1 o posterior) |
| macOS Intel | `fiscalapi-osx-x64.tar.gz` | macOS 15 o más nuevo, Mac con procesador Intel |

Los requisitos de Windows y macOS son los de .NET 10
([sistemas soportados](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)).

Los comandos de abajo descargan la **última versión publicada**. Para una versión fija, cambia
`releases/latest/download` por `releases/download/v1.2.3`.

**Verifica siempre el SHA-256 antes de ejecutar el binario.** El comando de verificación debe decir `OK`; si dice
`FAILED`, borra lo que descargaste y vuelve a bajarlo.

## Linux

```bash
base=https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download
curl -fsSLO "$base/fiscalapi-linux-x64.tar.gz"
curl -fsSLO "$base/SHA256SUMS"
grep ' fiscalapi-linux-x64.tar.gz$' SHA256SUMS | sha256sum -c -   # debe decir: OK
tar -xzf fiscalapi-linux-x64.tar.gz
sudo install -m 0755 fiscalapi /usr/local/bin/fiscalapi
fiscalapi --version
```

Sin permisos de administrador, instálalo en tu usuario: `install -D -m 0755 fiscalapi ~/.local/bin/fiscalapi` y
verifica que `~/.local/bin` esté en tu `PATH`.

## macOS

En una Mac con Apple Silicon usa `fiscalapi-osx-arm64.tar.gz`; en una Intel, `fiscalapi-osx-x64.tar.gz`
(`uname -m` dice `arm64` o `x86_64`).

```bash
package=fiscalapi-osx-arm64.tar.gz
base=https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download
curl -fsSLO "$base/$package"
curl -fsSLO "$base/SHA256SUMS"
grep " $package\$" SHA256SUMS | shasum -a 256 -c -   # debe decir: OK
tar -xzf "$package"
sudo mkdir -p /usr/local/bin
sudo install -m 0755 fiscalapi /usr/local/bin/fiscalapi
fiscalapi --version
```

El binario no está firmado ni notarizado por Apple. Descargado con `curl` corre sin más. Si lo bajaste con el
navegador, Gatekeeper lo bloquea la primera vez; quítale la marca de cuarentena:

```bash
xattr -d com.apple.quarantine /usr/local/bin/fiscalapi
```

## Windows (PowerShell)

```powershell
$base = 'https://github.com/FiscalAPI/fiscalapi-cli/releases/latest/download'
Invoke-WebRequest "$base/fiscalapi-win-x64.zip" -OutFile fiscalapi-win-x64.zip
Invoke-WebRequest "$base/SHA256SUMS" -OutFile SHA256SUMS
$expected = (Select-String -Path SHA256SUMS -Pattern ' fiscalapi-win-x64\.zip$').Line.Split(' ')[0]
$actual = (Get-FileHash fiscalapi-win-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw "El SHA-256 no coincide: $actual" } else { 'SHA-256 OK' }

$dir = Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi'
Expand-Archive fiscalapi-win-x64.zip -DestinationPath $dir -Force
```

### Agregar `fiscalapi` al PATH

El bloque siguiente agrega la carpeta al `Path` de tu usuario. Lo lee y lo escribe en el registro como
`REG_EXPAND_SZ`, sin expandir las variables que ya tenga (`%USERPROFILE%`…), y no lo duplica si ya está:

```powershell
$dir = Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi'
$environment = Get-Item 'HKCU:\Environment'
$userPath = $environment.GetValue('Path', '', 'DoNotExpandEnvironmentNames')
if (($userPath -split ';') -notcontains $dir) {
    $newPath = (@($userPath.TrimEnd(';'), $dir) | Where-Object { $_ }) -join ';'
    Set-ItemProperty -Path 'HKCU:\Environment' -Name Path -Value $newPath -Type ExpandString
    # Avisa a Windows del cambio para que las terminales nuevas lo vean (no crea ninguna variable).
    [Environment]::SetEnvironmentVariable('FISCALAPI_CLI_PATH_REFRESH', [NullString]::Value, 'User')
}
```

Si prefieres hacerlo a mano: busca "Editar las variables de entorno de esta cuenta" en el menú Inicio, abre `Path`,
elige **Nuevo** y escribe `%LOCALAPPDATA%\Programs\fiscalapi`.

Abre una terminal nueva y corre `fiscalapi --version`.

El binario no lleva firma Authenticode. Si bajaste el zip con el navegador, SmartScreen puede advertir al abrirlo
("Windows protegió su PC"); quita la marca de descarga con:

```powershell
Unblock-File (Join-Path $env:LOCALAPPDATA 'Programs\fiscalapi\fiscalapi.exe')
```

## Actualizar

Repite la instalación de tu sistema: el binario nuevo reemplaza al anterior. Tus credenciales guardadas con
`fiscalapi login` se conservan, porque viven en el almacén del sistema y no junto al binario.

### Aviso de versión nueva

Mientras corre un comando, `fiscalapi` pregunta a GitHub por el último release publicado, **como mucho una vez cada
24 horas** y sin enviar tus credenciales. Si hay uno más nuevo, al terminar el comando escribe en stderr:

```text

Hay una versión nueva de fiscalapi: 0.2.0 (tienes 0.1.0).
Descárgala en https://github.com/FiscalAPI/fiscalapi-cli/releases/tag/v0.2.0
```

- La consulta tarda como mucho 2 segundos. Si GitHub no responde, falla o devuelve algo inesperado, no se muestra nada
  y el comando termina igual, con su mismo código de salida.
- Solo se anuncian releases publicados y estables: nunca un borrador ni una prerelease (`1.0.0-rc.1`).
- Con Ctrl+C solo se muestra si el aviso ya había llegado (por ejemplo, durante un `fiscalapi listen` largo).

El aviso **no corre**:

| Caso | Por qué |
|---|---|
| Con `--json` | La salida es para otra herramienta. |
| Con stderr redirigido (`2> log.txt`) | No ensucia el log de un script. |
| Con `CI` definida (`true` o `1`, como en GitHub Actions y la mayoría de los CI) | En un CI no hay quién lo lea. |
| Con `FISCALAPI_NO_UPDATE_CHECK=1` (o `true`) | Para apagarlo siempre. |

Cualquier valor de `CI` o de `FISCALAPI_NO_UPDATE_CHECK` lo apaga, salvo vacío, `0` o `false`. Para apagarlo de forma
permanente:

```bash
echo 'export FISCALAPI_NO_UPDATE_CHECK=1' >> ~/.bashrc    # o ~/.zshrc
```

```powershell
[Environment]::SetEnvironmentVariable('FISCALAPI_NO_UPDATE_CHECK', '1', 'User')
```

La fecha de la última consulta, sin ningún secreto, se guarda en:

| Sistema | Archivo |
|---|---|
| Windows | `%LOCALAPPDATA%\fiscalapi-cli\update-check.json` |
| macOS | `~/Library/Application Support/fiscalapi-cli/update-check.json` |
| Linux | `$XDG_STATE_HOME/fiscalapi-cli/update-check.json` (por omisión `~/.local/state/fiscalapi-cli/…`) |

Borrarlo solo adelanta la siguiente consulta. Si la carpeta no se puede escribir, el CLI no consulta.

## Desinstalar

1. Borra las credenciales guardadas de cada ambiente que usaste, **antes** de borrar el binario:

   ```bash
   fiscalapi logout
   fiscalapi logout --live
   fiscalapi logout --api-base https://localhost:5001   # si usaste otra URL base
   ```

2. Borra el binario y la carpeta de estado:

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
