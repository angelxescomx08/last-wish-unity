param([string]$UnityEditorRoot = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
foreach ($project in @('UnityCompile/UnityCompile.csproj', 'DomainSmoke/DomainSmoke.csproj')) {
    $projectPath = Join-Path $PSScriptRoot $project
    dotnet restore $projectPath --configfile (Join-Path $PSScriptRoot 'NuGet.Config') -p:UnityEditorRoot=$UnityEditorRoot --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $project" }
    dotnet build $projectPath --no-restore -p:UnityEditorRoot=$UnityEditorRoot --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $project" }
}
dotnet run --project (Join-Path $PSScriptRoot 'DomainSmoke/DomainSmoke.csproj') --no-build -p:UnityEditorRoot=$UnityEditorRoot
if ($LASTEXITCODE -ne 0) { throw 'Domain checks failed' }
