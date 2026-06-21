param(
    [string]$version = ""
)

$project = "src\LegacyEditor\LegacyEditor.csproj"

if (-not $version) {
    $version = Select-Xml -Path $project -XPath "//Version" | Select-Object -ExpandProperty Node | Select-Object -ExpandProperty InnerText
}

$output = "LegacyEditor-v$version-Windows"

Write-Host "Building LegacyEditor v$version..." -ForegroundColor Cyan

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded `
    -o $output

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Remove-Item "$output\*.pdb" -ErrorAction SilentlyContinue
Remove-Item "$output\*.xml" -ErrorAction SilentlyContinue

Write-Host "Done! Output in: $output" -ForegroundColor Green
