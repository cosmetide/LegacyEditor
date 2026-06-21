param(
    [string]$version = "0.0.1"
)

$project = "src\LegacyEditor\LegacyEditor.csproj"
$output = "LegacyEditor-v$version-Windows"

Write-Host "Building LegacyEditor v$version..." -ForegroundColor Cyan

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded `
    -p:Version=$version `
    -o $output

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Remove-Item "$output\*.pdb" -ErrorAction SilentlyContinue
Remove-Item "$output\*.xml" -ErrorAction SilentlyContinue

Write-Host "Done! Output in: $output" -ForegroundColor Green
