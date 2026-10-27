param([string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'P4UnityLocator'))
$ErrorActionPreference = 'Stop'
# Run only after P4V has been closed normally; never terminate the user's client.
if (Get-Process p4v -ErrorAction SilentlyContinue) {
    throw '请先正常关闭 P4V，再运行此安装脚本，避免运行中的 P4V 覆盖设置。也可直接在 P4V 中导入 P4V-UnityLocator.xml。'
}

$settingsFile = Join-Path $env:USERPROFILE '.p4qt\customtools.xml'
$importFile = Join-Path $installRoot 'P4V-UnityLocator.xml'
$backupRoot = Join-Path $installRoot ('Backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
$hadFile = Test-Path -LiteralPath $settingsFile
if ($hadFile) { Copy-Item -LiteralPath $settingsFile -Destination (Join-Path $backupRoot 'customtools.xml') }
try {
    [xml]$incoming = Get-Content -LiteralPath $importFile -Raw -Encoding UTF8
    if ($hadFile -and (Get-Item -LiteralPath $settingsFile).Length -gt 0) {
        [xml]$settings = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8
    } else { [xml]$settings = '<CustomToolDefList varName="customtooldeflist" />' }
    if ($settings.DocumentElement.Name -ne 'CustomToolDefList') { throw '未知的 P4V 自定义工具格式，未修改。' }
    $toolName = $incoming.CustomToolDefList.CustomToolDef.Definition.Name
    $existing = @($settings.CustomToolDefList.CustomToolDef | Where-Object { $_.Definition.Name -eq $toolName })
    if ($existing.Count -eq 0) {
        $null = $settings.DocumentElement.AppendChild($settings.ImportNode($incoming.CustomToolDefList.CustomToolDef, $true))
        $settings.Save($settingsFile)
        [xml]$checked = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8
        if (-not ($checked.CustomToolDefList.CustomToolDef | Where-Object { $_.Definition.Name -eq $toolName })) { throw '配置校验失败。' }
    }
    Write-Host '右键入口已安装。重新打开 P4V 后生效。材质双击关联请按 README 设置。'
    Write-Host ('备份：' + $backupRoot)
} catch {
    if ($hadFile) { Copy-Item -LiteralPath (Join-Path $backupRoot 'customtools.xml') -Destination $settingsFile -Force }
    elseif (Test-Path -LiteralPath $settingsFile) { Remove-Item -LiteralPath $settingsFile }
    throw
}
