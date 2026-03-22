param(
    [ValidateSet('portable', 'singlefile')]
    [string]$Mode = 'portable'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dotnet = 'C:\Users\18910\.dotnet\dotnet.exe'
$project = Join-Path $root 'VisionFlowStudio.App\VisionFlowStudio.App.csproj'
$publishRoot = Join-Path $root 'artifacts\publish'
$output = Join-Path $publishRoot $Mode

if (!(Test-Path $dotnet)) {
    throw '.NET SDK 未安装到预期路径：C:\Users\18910\.dotnet\dotnet.exe'
}

New-Item -ItemType Directory -Force -Path $output | Out-Null

$baseArgs = @(
    'publish',
    $project,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-o', $output,
    '/p:PublishReadyToRun=true',
    '/p:DebugType=None',
    '/p:DebugSymbols=false'
)

if ($Mode -eq 'singlefile') {
    $baseArgs += @(
        '/p:PublishSingleFile=true',
        '/p:IncludeNativeLibrariesForSelfExtract=true'
    )
}
else {
    $baseArgs += '/p:PublishSingleFile=false'
}

& $dotnet @baseArgs

Write-Host ''
Write-Host ('Publish complete: ' + $output)
