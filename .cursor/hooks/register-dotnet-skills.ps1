$null = [Console]::In.ReadToEnd()

$root = $env:CURSOR_PROJECT_DIR
if ([string]::IsNullOrWhiteSpace($root)) {
    $root = (Get-Location).Path
}

$skillsRoot = Join-Path $root '.github\dotnet-skills\plugins'
if (-not (Test-Path -LiteralPath $skillsRoot -PathType Container)) {
    '{}'
    exit 0
}

$plugins = @(
    'dotnet',
    'dotnet-data',
    'dotnet-aspnetcore',
    'dotnet-blazor',
    'dotnet-maui',
    'dotnet-test',
    'dotnet-msbuild',
    'dotnet-upgrade',
    'dotnet-nuget',
    'dotnet-diag'
)

$paths = @(
    foreach ($plugin in $plugins) {
        $pluginPath = Join-Path $skillsRoot $plugin
        if (Test-Path -LiteralPath $pluginPath -PathType Container) {
            (Resolve-Path -LiteralPath $pluginPath).Path
        }
    }
)

if ($paths.Count -eq 0) {
    '{}'
    exit 0
}

@{ pluginPaths = $paths } | ConvertTo-Json -Compress
