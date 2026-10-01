$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$runtimeRoot = Join-Path $projectRoot 'Assets/Presentation'
$output = Join-Path $projectRoot 'Temp/PresentationVerification'
New-Item -ItemType Directory -Force -Path $output | Out-Null

# Reuse the current project's real Unity/package assemblies without importing the runtime scripts.
[xml]$project = Get-Content (Join-Path $projectRoot 'Assembly-CSharp.csproj')
$references = @($project.Project.ItemGroup.Reference.HintPath | Where-Object { $_ } | ForEach-Object {
    if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $projectRoot $_ }
} | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -Unique)
$core = $references | Where-Object { [IO.Path]::GetFileName($_) -eq 'UnityEngine.CoreModule.dll' } | Select-Object -First 1
$editorData = Split-Path (Split-Path (Split-Path $core -Parent) -Parent) -Parent
$compiler = Join-Path $editorData 'DotNetSdkRoslyn/csc.dll'
$mono = Join-Path $editorData 'MonoBleedingEdge/bin/mono.exe'
$dll = Join-Path $output 'PresentationRewrite.dll'
$response = @('-nologo', '-target:library', '-langversion:9.0', '-nostdlib+', ('-out:"{0}"' -f $dll))
$response += $references | ForEach-Object { '-r:"{0}"' -f $_ }
$response += Get-ChildItem (Join-Path $runtimeRoot 'Scripts') -Recurse -Filter '*.cs' | ForEach-Object { '"{0}"' -f $_.FullName }
$rsp = Join-Path $output 'compile.rsp'
Set-Content -LiteralPath $rsp -Value $response -Encoding utf8
& dotnet $compiler "@$rsp"
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed.' }
Write-Output 'Runtime compilation passed against the current Unity assemblies.'

$exe = Join-Path $output 'Checks.exe'
$testResponse = @('-nologo', '-target:exe', '-langversion:9.0', '-nostdlib+', ('-out:"{0}"' -f $exe), ('-r:"{0}"' -f $dll))
$testResponse += $references | ForEach-Object { '-r:"{0}"' -f $_ }
$testResponse += '"{0}"' -f (Join-Path $PSScriptRoot 'Checks.cs')
$testRsp = Join-Path $output 'checks.rsp'
Set-Content -LiteralPath $testRsp -Value $testResponse -Encoding utf8
& dotnet $compiler "@$testRsp"
if ($LASTEXITCODE -ne 0) { throw 'Check compilation failed.' }
foreach ($name in @('UnityEngine.CoreModule.dll', 'UnityEngine.SharedInternalsModule.dll', 'Newtonsoft.Json.dll', 'Unity.TextMeshPro.dll', 'UnityEngine.UI.dll', 'UnityEngine.UIModule.dll')) {
    $dependency = $references | Where-Object { [IO.Path]::GetFileName($_) -eq $name } | Select-Object -First 1
    Copy-Item -LiteralPath $dependency -Destination $output -Force
}
& $mono $exe
if ($LASTEXITCODE -ne 0) { throw 'Behavior checks failed.' }

