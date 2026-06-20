param(
    [string]$Root = (Join-Path $PSScriptRoot "..\src\Clients\Shared\UI")
)

$resourcesRoot = Join-Path $Root "Resources"
$issues = [System.Collections.Generic.List[object]]::new()

Get-ChildItem -Path $Root -Recurse -Include "*.razor", "*.razor.cs" | ForEach-Object {
    $file = $_
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.UTF8Encoding]::new($false))

    $localizer = $null
    if ($content -match 'IStringLocalizer<(\w+)>') {
        $localizer = $Matches[1]
    }

    if ([string]::IsNullOrEmpty($localizer)) {
        return
    }

    $resxPath = Get-ChildItem -Path $resourcesRoot -Recurse -Filter ($localizer + ".resx") |
        Where-Object { $_.Name -notmatch '\.en\.resx$' } |
        Select-Object -First 1

    if ($null -eq $resxPath) {
        return
    }

    $resx = [xml][System.IO.File]::ReadAllText($resxPath.FullName, [System.Text.UTF8Encoding]::new($false))
    $keys = @($resx.root.data | ForEach-Object { $_.name })

    $pattern = 'L\["([^"]+)"\]'
    foreach ($match in [regex]::Matches($content, $pattern)) {
        $key = $match.Groups[1].Value
        if ($keys -notcontains $key) {
            $issues.Add([pscustomobject]@{
                Resx  = $resxPath.Name
                Key   = $key
                File  = $file.Name
            })
        }
    }
}

$issues |
    Sort-Object Resx, Key -Unique |
    Format-Table -AutoSize

Write-Output ""
Write-Output "Total missing: $(($issues | Sort-Object Resx, Key -Unique).Count)"
