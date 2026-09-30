$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationRoot = Join-Path $projectRoot 'Builds/ReferenceGuestValidationProject'
New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null
# Share asset GUIDs with the open editor before the isolated import creates any.
$texturePath = Join-Path $projectRoot 'Assets/_Project/Resources/NO404/Characters/FirstGuest/Worker_BaseColor.png.meta'
if (-not (Test-Path -LiteralPath $texturePath)) {
    $assetGuid = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($texturePath, "fileFormatVersion: 2`nguid: $assetGuid`n")
}
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings', 'Library/PackageCache')) {
    $sourceFolder = Join-Path $projectRoot $folder
    $targetFolder = Join-Path $validationRoot $folder
    & robocopy $sourceFolder $targetFolder /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw "Copy failed: $folder" }
}
New-Item -ItemType Directory -Force -Path (Join-Path $validationRoot 'Logs') | Out-Null
Write-Output "Validation project ready: $validationRoot"
