param([string]$EditorData = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskOutput = Join-Path $taskRoot 'Temp\MiaValidation\Compile'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskCompiler = Get-ChildItem -LiteralPath (Join-Path $EditorData 'DotNetSdk\sdk') -Directory |
    Sort-Object Name -Descending | Select-Object -First 1
$taskCsc = Join-Path $taskCompiler.FullName 'Roslyn\bincore\csc.dll'
$taskDotnet = Join-Path $EditorData 'DotNetSdk\dotnet.exe'

# Managed\UnityEngine holds both the engine and the editor module assemblies. Referencing the
# duplicates under Managed\ as well makes every shared type ambiguous (CS0433), so keep them apart.
$taskModules = Join-Path $EditorData 'Managed\UnityEngine'
$taskEngineRefs = @(Get-ChildItem (Join-Path $EditorData 'NetStandard\ref\2.1.0') -Filter '*.dll') +
    @(Get-ChildItem $taskModules -Filter 'UnityEngine*.dll')
$taskEditorRefs = @(Get-ChildItem $taskModules -Filter 'UnityEditor*.dll')

# Package assemblies (URP among them) only exist once the editor has compiled the project at least
# once. Without them the editor scripts cannot resolve UnityEngine.Rendering.Universal.
$taskPackageDir = Join-Path $taskRoot 'Library\ScriptAssemblies'
$taskPackageRefs = @()
if (Test-Path $taskPackageDir) {
    # Assembly-CSharp is this project's own scripts as the editor last compiled them; referencing it
    # alongside the MiaCourt.dll built above from the working tree makes every game type ambiguous.
    $taskPackageRefs = @(Get-ChildItem $taskPackageDir -Filter '*.dll' |
        Where-Object { $_.Name -notlike 'Assembly-CSharp*' })
}
$taskHasUrp = @($taskPackageRefs | Where-Object { $_.Name -like 'Unity.RenderPipelines.Universal*' }).Count -gt 0

function Invoke-Csc([string]$Name, [string[]]$Arguments) {
    $response = Join-Path $taskOutput ($Name + '.rsp')
    [IO.File]::WriteAllLines($response, $Arguments, (New-Object Text.UTF8Encoding $false))
    # Out-Host keeps diagnostics on screen instead of folding them into this function's return value.
    & $taskDotnet exec $taskCsc '-noconfig' ('@' + $response) | Out-Host
    return $LASTEXITCODE
}

$taskCommon = @('-nologo', '-target:library', '-nostdlib+', '-langversion:9.0')
$taskRuntime = Join-Path $taskOutput 'MiaCourt.dll'
$taskArgs = $taskCommon +
    @($taskEngineRefs | ForEach-Object { '-r:"' + $_.FullName + '"' }) +
    @('-out:"' + $taskRuntime + '"') +
    @(Get-ChildItem (Join-Path $taskRoot 'Assets\Scripts') -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' })
if ((Invoke-Csc 'runtime' $taskArgs) -ne 0) { exit $LASTEXITCODE }
Write-Output 'PASS: game scripts compile against the installed Unity assemblies.'

$taskEditorFiles = @(Get-ChildItem (Join-Path $taskRoot 'Assets\Editor') -Filter '*.cs')
if ($taskEditorFiles.Count -eq 0) { exit 0 }
if (-not $taskHasUrp) {
    Write-Output ('SKIP: editor scripts need the URP package assemblies in Library\ScriptAssemblies, ' +
        'which the Unity editor writes on its first successful compile of this project. Run the editor once, then re-run this script.')
    exit 0
}
$taskArgs = $taskCommon + @('-define:UNITY_EDITOR,UNITY_EDITOR_WIN') +
    @($taskEngineRefs + $taskEditorRefs + $taskPackageRefs | ForEach-Object { '-r:"' + $_.FullName + '"' }) +
    @('-r:"' + $taskRuntime + '"') +
    @('-out:"' + (Join-Path $taskOutput 'MiaCourt.Editor.dll') + '"') +
    @($taskEditorFiles | ForEach-Object { '"' + $_.FullName + '"' })
if ((Invoke-Csc 'editor' $taskArgs) -ne 0) { exit $LASTEXITCODE }
Write-Output 'PASS: editor setup and build scripts compile.'
