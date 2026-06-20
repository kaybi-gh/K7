# Repairs UTF-8 mojibake in .resx value strings (e.g. TÃ©lÃ©chargement -> Téléchargement).
param(
    [string]$Root = (Join-Path $PSScriptRoot "..\src\Clients\Shared\UI\Resources")
)

function Repair-Mojibake {
    param([string]$Value)

    if ($Value.IndexOf([char]0x00C3) -lt 0) {
        return $Value
    }

    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $bytes = $latin1.GetBytes($Value)
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

$utf8 = [System.Text.UTF8Encoding]::new($false)
$files = Get-ChildItem -Path $Root -Filter "*.resx" -Recurse |
    Where-Object { $_.Name -notmatch '\.en\.resx$' }

$changedFiles = 0
$changedValues = 0

foreach ($file in $files) {
    $content = [System.IO.File]::ReadAllText($file.FullName, $utf8)
    $original = $content
    $fileChanges = 0

    $updated = [regex]::Replace($content, '(<value[^>]*>)(.*?)(</value>)', {
        param($match)
        $prefix = $match.Groups[1].Value
        $value = $match.Groups[2].Value
        $suffix = $match.Groups[3].Value
        $fixed = Repair-Mojibake $value
        if ($fixed -ne $value) {
            $script:fileChanges++
        }
        return "$prefix$fixed$suffix"
    })

    if ($updated -ne $original) {
        $changedFiles++
        $changedValues += $fileChanges
        [System.IO.File]::WriteAllText($file.FullName, $updated, $utf8)
        Write-Output "$($file.Name) ($fileChanges values)"
    }
}

Write-Output ""
Write-Output "Repaired $changedFiles files, $changedValues value strings."
