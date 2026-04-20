param(
    [string]$CommandPath = "C:\CFAPI\FileSyncUploader.exe"
)

$keyPath = "Registry::HKEY_CLASSES_ROOT\*\shell\FileSyncUpload"
$commandKeyPath = "$keyPath\command"

New-Item -Path $keyPath -Force | Out-Null
Set-ItemProperty -Path $keyPath -Name "(default)" -Value "Upload to FileSync Cloud"
Set-ItemProperty -Path $keyPath -Name "Icon" -Value "shell32.dll,167"

New-Item -Path $commandKeyPath -Force | Out-Null
Set-ItemProperty -Path $commandKeyPath -Name "(default)" -Value "`"$CommandPath`" `"%1`""

Write-Host "Context menu registered."
