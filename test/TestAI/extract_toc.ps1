$OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Extract-DocxText([string]$path, [int]$maxLines = 200) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
    $entry = $zip.GetEntry("word/document.xml")
    $stream = $entry.Open()
    $reader = New-Object System.IO.StreamReader($stream)
    $xml = [xml]$reader.ReadToEnd()
    $reader.Close()
    $zip.Dispose()

    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main")
    $paragraphs = $xml.SelectNodes("//w:p", $ns)

    $count = 0
    foreach ($p in $paragraphs) {
        if ($count -ge $maxLines) { break }
        $texts = $p.SelectNodes(".//w:t", $ns)
        $parts = @()
        foreach ($t in $texts) { $parts += $t.'#text' }
        $line = ($parts -join "").Trim()
        if ($line) {
            Write-Output $line
            $count++
        }
    }
}

$docxDir = Join-Path $PSScriptRoot "docx"
$files = Get-ChildItem -Path $docxDir -Filter "*.docx"

foreach ($f in $files) {
    Write-Output "===== $($f.Name) ====="
    Extract-DocxText $f.FullName 200
    Write-Output ""
}
