Add-Type -AssemblyName System.Design
[void][Reflection.Assembly]::LoadWithPartialName('System.Resources.Tools')

$resourcesFile = 'Resources.resx'
$designerFile = 'Resources.Designer.cs'
$namespace = 'WpfHexEditor.Sample.HexEditor.Properties'
$className = 'Resources'

$writer = New-Object System.IO.StreamWriter($designerFile, $false, [System.Text.Encoding]::UTF8)

try {
    [System.Resources.Tools.StronglyTypedResourceBuilder]::Generate(
        $resourcesFile,
        $namespace,
        $namespace,
        $className,
        $writer,
        $true
    )
    $writer.Close()
    Write-Host 'Resources.Designer.cs regenerated successfully'
} catch {
    Write-Host "Error: $_"
    $writer.Close()
}
