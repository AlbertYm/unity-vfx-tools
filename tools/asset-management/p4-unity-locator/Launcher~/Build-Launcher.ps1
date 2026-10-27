param([string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'P4UnityLocator'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '需要 Windows .NET Framework 4.x 编译器。' }
New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
$exe = Join-Path $InstallRoot 'P4UnityLocator.exe'
if (Test-Path -LiteralPath $exe) {
    $backup = Join-Path $InstallRoot ('Backups\build-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $exe -Destination $backup
}
& $compiler /nologo /target:winexe /reference:System.Windows.Forms.dll ('/out:' + $exe) (Join-Path $PSScriptRoot 'P4UnityLocator.cs')
if ($LASTEXITCODE -ne 0) { throw '编译失败；未生成 P4V 配置。旧文件备份位于 Backups。' }
$escaped = [Security.SecurityElement]::Escape($exe)
$xml = @"
<?xml version="1.0" encoding="UTF-8"?>
<!--perforce-xml-version=1.0-->
<CustomToolDefList varName="customtooldeflist">
 <CustomToolDef>
  <Definition>
   <Name>在 Unity 中定位资源</Name>
   <Command>$escaped</Command>
   <Arguments>%f</Arguments>
   <Shortcut></Shortcut>
  </Definition>
  <Console><CloseOnExit>true</CloseOnExit></Console>
  <AddToContext>true</AddToContext>
 </CustomToolDef>
</CustomToolDefList>
"@
[IO.File]::WriteAllText((Join-Path $InstallRoot 'P4V-UnityLocator.xml'), $xml, [Text.UTF8Encoding]::new($false))
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
[IO.File]::WriteAllText((Join-Path $InstallRoot 'SHA256.txt'), ($hash + '  P4UnityLocator.exe'), [Text.UTF8Encoding]::new($false))
Write-Host ('启动器：' + $exe)
Write-Host ('P4V 导入文件：' + (Join-Path $InstallRoot 'P4V-UnityLocator.xml'))
Write-Host '本步骤仅构建程序和配置文件，尚未修改 P4V。请按 README 完成材质双击关联。'