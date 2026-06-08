<#
.SYNOPSIS
    Converts the diploma-works Excel file into diplomas.json.

.DESCRIPTION
    Reads the table (sheet "Dannye", range A3:I68 - header in row 3, data in
    rows 4-68) directly from the xlsx (no third-party modules) and produces a
    JSON array of Diploma objects. Output is written to data/diplomas.json and
    copied to the bundled fallback DiplomasViewer/wwwroot/sample-data/diplomas.json.

    NOTE: keep this script ASCII-only so Windows PowerShell 5.1 parses it
    correctly regardless of file encoding. Cyrillic content flows through from
    the xlsx at runtime via UTF-8 streams.

.EXAMPLE
    powershell -File ./tools/Convert-Xlsx.ps1
#>
[CmdletBinding()]
param(
    [string]$XlsxPath,
    [string]$OutDataPath = (Join-Path $PSScriptRoot '..\data\diplomas.json'),
    [string]$OutFallbackPath = (Join-Path $PSScriptRoot '..\DiplomasViewer\wwwroot\sample-data\diplomas.json'),
    [int]$FirstDataRow = 4,
    [int]$LastDataRow = 68
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Auto-detect the xlsx in the repo root if not supplied (avoids a Cyrillic literal).
if (-not $XlsxPath) {
    $root = Join-Path $PSScriptRoot '..'
    $candidate = Get-ChildItem -Path $root -Filter '*.xlsx' -File | Select-Object -First 1
    if (-not $candidate) { throw "No .xlsx file found in $root" }
    $XlsxPath = $candidate.FullName
}
$XlsxPath = (Resolve-Path $XlsxPath).Path
Write-Host "Reading: $XlsxPath"

$zip = [System.IO.Compression.ZipFile]::OpenRead($XlsxPath)
try {
    function Read-Entry([string]$name) {
        $entry = $zip.Entries | Where-Object { $_.FullName -eq $name }
        if (-not $entry) { throw "Archive has no '$name'." }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Close() }
    }

    # --- Shared strings: index -> text ---
    [xml]$ssXml = Read-Entry 'xl/sharedStrings.xml'
    $strings = New-Object System.Collections.Generic.List[string]
    foreach ($si in $ssXml.sst.si) {
        if ($si.t -is [string]) {
            $strings.Add([string]$si.t)
        }
        elseif ($si.t.'#text') {
            $strings.Add([string]$si.t.'#text')
        }
        elseif ($si.r) {
            # rich text: join runs
            $text = ($si.r | ForEach-Object { [string]$_.t.'#text' }) -join ''
            $strings.Add($text)
        }
        else {
            $strings.Add('')
        }
    }

    # --- Worksheet ---
    [xml]$sheet = Read-Entry 'xl/worksheets/sheet1.xml'

    # xlsx column -> model field
    $columnMap = @{
        'A' = 'group'; 'B' = 'student'; 'C' = 'topic'; 'D' = 'supervisor';
        'E' = 'description'; 'F' = 'repoUrl'; 'G' = 'installUrl';
        'H' = 'demoUrl'; 'I' = 'year'
    }

    function Get-CellText($cell) {
        if (-not $cell) { return '' }
        $v = [string]$cell.v
        if ($cell.t -eq 's') {
            $idx = [int]$v
            if ($idx -ge 0 -and $idx -lt $strings.Count) { return $strings[$idx] }
            return ''
        }
        if ($cell.t -eq 'inlineStr') { return [string]$cell.is.t.'#text' }
        return $v
    }

    $result = New-Object System.Collections.Generic.List[object]
    foreach ($row in $sheet.worksheet.sheetData.row) {
        $rowNum = [int]$row.r
        if ($rowNum -lt $FirstDataRow -or $rowNum -gt $LastDataRow) { continue }

        $record = [ordered]@{
            id = ([guid]::NewGuid()).ToString('N')
            group = ''; student = ''; topic = ''; supervisor = '';
            description = ''; repoUrl = ''; installUrl = ''; demoUrl = ''; year = $null
        }

        foreach ($cell in $row.c) {
            $col = ($cell.r -replace '\d', '')   # "B4" -> "B"
            $field = $columnMap[$col]
            if (-not $field) { continue }
            $text = (Get-CellText $cell).Trim()
            if ($field -eq 'year') {
                $parsed = 0
                $record.year = if ([int]::TryParse($text, [ref]$parsed)) { $parsed } else { $null }
            }
            else {
                $record[$field] = $text
            }
        }

        # skip fully empty rows
        if ($record.student -or $record.topic) {
            $result.Add([pscustomobject]$record)
        }
    }

    Write-Host "Records found: $($result.Count)"

    $json = $result | ConvertTo-Json -Depth 5
    # ConvertTo-Json does not wrap a single element in an array - force an array
    if ($result.Count -eq 1) { $json = "[$json]" }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    foreach ($outPath in @($OutDataPath, $OutFallbackPath)) {
        $dir = Split-Path -Parent $outPath
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        [System.IO.File]::WriteAllText($outPath, $json, $utf8)
        Write-Host "Written: $outPath"
    }
}
finally {
    $zip.Dispose()
}
