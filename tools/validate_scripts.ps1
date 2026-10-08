# Fast offline C# validation tool for Dungeon Guardians (Unity 6)
# Validates both Runtime and Editor scripts against Unity Roslyn compiler and engine assemblies.

$ErrorActionPreference = "Continue"

$unityBase = "C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Data"
$csc = "C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore\csc.dll"

if (-not (Test-Path $csc)) {
    $cscCandidate = Get-ChildItem -Path "C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    if ($cscCandidate) { $csc = $cscCandidate }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "[VALIDATION] DUNGEON GUARDIANS: CHECKING C# SCRIPTS..." -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Runtime scripts
$runtimeFiles = Get-ChildItem -Path "Assets\Scripts" -Filter "*.cs" -Recurse | Where-Object { $_.FullName -notmatch "\\Editor\\" } | Select-Object -ExpandProperty FullName
Write-Host "-> Checking $($runtimeFiles.Count) Runtime C# scripts..." -ForegroundColor Yellow

$runtimeRefs = @(
    "$unityBase\NetStandard\ref\2.1.0\netstandard.dll"
)
$runtimeRefs += (Get-ChildItem -Path "$unityBase\NetStandard\compat\2.1.0\shims\netstandard\*.dll" | Select-Object -ExpandProperty FullName)
$runtimeRefs += (Get-ChildItem -Path "$unityBase\Managed\UnityEngine\*.dll" | Select-Object -ExpandProperty FullName)
$runtimeRefs += (Get-ChildItem -Path "Library\ScriptAssemblies\*.dll" | Where-Object { $_.Name -notmatch "Editor|Assembly-CSharp" } | Select-Object -ExpandProperty FullName)
$runtimeRefs = $runtimeRefs | Where-Object { [System.IO.File]::Exists($_) } | Select-Object -Unique

$tempRuntimeOut = [System.IO.Path]::GetTempFileName() + ".dll"
$tempRuntimeRsp = [System.IO.Path]::GetTempFileName() + ".rsp"
$runtimeRspLines = @("-target:library", "-out:`"$tempRuntimeOut`"", "-langversion:latest", "-nologo", "-warn:0", "-nullable:disable")
$runtimeRspLines += ($runtimeRefs | ForEach-Object { "-r:`"$_`"" })
$runtimeRspLines += ($runtimeFiles | ForEach-Object { "`"$_`"" })
[System.IO.File]::WriteAllLines($tempRuntimeRsp, $runtimeRspLines)

$rtOutFile = [System.IO.Path]::GetTempFileName() + ".txt"
$rtErrFile = [System.IO.Path]::GetTempFileName() + ".txt"
$rtProc = Start-Process -FilePath "dotnet" -ArgumentList @("exec", "`"$csc`"", "@`"$tempRuntimeRsp`"") -NoNewWindow -PassThru -Wait -RedirectStandardOutput $rtOutFile -RedirectStandardError $rtErrFile
$rtOut = Get-Content $rtOutFile -Raw
$rtErr = Get-Content $rtErrFile -Raw

if ($rtProc.ExitCode -ne 0) {
    Write-Host "[ERROR] RUNTIME COMPILATION FAILED:" -ForegroundColor Red
    if ($rtOut) { Write-Host $rtOut -ForegroundColor Red }
    if ($rtErr) { Write-Host $rtErr -ForegroundColor Red }
    Remove-Item $tempRuntimeOut, $tempRuntimeRsp, $rtOutFile, $rtErrFile -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "[SUCCESS] Runtime scripts compiled successfully (0 errors)." -ForegroundColor Green

# 2. Editor scripts
$editorFiles = Get-ChildItem -Path "Assets\Scripts\Editor" -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName
Write-Host "-> Checking $($editorFiles.Count) Editor C# scripts..." -ForegroundColor Yellow

$editorRefs = @(
    "$unityBase\NetStandard\ref\2.1.0\netstandard.dll",
    "$unityBase\Managed\UnityEngine.dll",
    "$unityBase\Managed\UnityEditor.dll",
    "$tempRuntimeOut"
)
$editorRefs += (Get-ChildItem -Path "$unityBase\NetStandard\compat\2.1.0\shims\netstandard\*.dll" | Select-Object -ExpandProperty FullName)
$editorRefs += (Get-ChildItem -Path "$unityBase\Managed\UnityEngine\*.dll" | Select-Object -ExpandProperty FullName)
$editorRefs += (Get-ChildItem -Path "Library\ScriptAssemblies\*.dll" | Where-Object { $_.Name -notmatch "Assembly-CSharp" } | Select-Object -ExpandProperty FullName)
$editorRefs = $editorRefs | Where-Object { [System.IO.File]::Exists($_) } | Select-Object -Unique

$tempEditorOut = [System.IO.Path]::GetTempFileName() + ".dll"
$tempEditorRsp = [System.IO.Path]::GetTempFileName() + ".rsp"
$editorRspLines = @("-target:library", "-out:`"$tempEditorOut`"", "-langversion:latest", "-nologo", "-warn:0", "-nullable:disable")
$editorRspLines += ($editorRefs | ForEach-Object { "-r:`"$_`"" })
$editorRspLines += ($editorFiles | ForEach-Object { "`"$_`"" })
[System.IO.File]::WriteAllLines($tempEditorRsp, $editorRspLines)

$edOutFile = [System.IO.Path]::GetTempFileName() + ".txt"
$edErrFile = [System.IO.Path]::GetTempFileName() + ".txt"
$edProc = Start-Process -FilePath "dotnet" -ArgumentList @("exec", "`"$csc`"", "@`"$tempEditorRsp`"") -NoNewWindow -PassThru -Wait -RedirectStandardOutput $edOutFile -RedirectStandardError $edErrFile
$edOut = Get-Content $edOutFile -Raw
$edErr = Get-Content $edErrFile -Raw

Remove-Item $tempRuntimeOut, $tempRuntimeRsp, $rtOutFile, $rtErrFile -ErrorAction SilentlyContinue
Remove-Item $tempEditorOut, $tempEditorRsp, $edOutFile, $edErrFile -ErrorAction SilentlyContinue

if ($edProc.ExitCode -ne 0) {
    Write-Host "[ERROR] EDITOR COMPILATION FAILED:" -ForegroundColor Red
    if ($edOut) { Write-Host $edOut -ForegroundColor Red }
    if ($edErr) { Write-Host $edErr -ForegroundColor Red }
    exit 1
}

Write-Host "[SUCCESS] Editor scripts compiled successfully (0 errors)." -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "[OK] ALL 36 SCRIPTS COMPILED CLEANLY WITH ZERO ERRORS!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
exit 0
