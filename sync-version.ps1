# sync-version.ps1
param (
    [string]$newVersion
)

# Read version config file with UTF-8 encoding
$versionConfig = Get-Content -Path "version.json" -Raw -Encoding UTF8 | ConvertFrom-Json

# If new version is provided, update it
if ($newVersion) {
    $versionConfig.version = $newVersion
    $versionConfig.releaseDate = Get-Date -Format "yyyy-MM-dd"
    $versionConfig | ConvertTo-Json -Depth 10 | Set-Content -Path "version.json" -Encoding UTF8
}

$version = $versionConfig.version
$releaseDate = $versionConfig.releaseDate

Write-Host "Synchronizing version: $version ($releaseDate)"

# Update all csproj files
$csprojFiles = Get-ChildItem -Path "src" -Recurse -Filter "*.csproj"
foreach ($file in $csprojFiles) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $content = $content -replace '<Version>(.*?)</Version>', "<Version>$version</Version>" -replace '<PackageVersion>(.*?)</PackageVersion>', "<PackageVersion>$version</PackageVersion>"
    Set-Content -Path $file.FullName -Value $content -Encoding UTF8
    Write-Host "Updated: $($file.FullName)"
}

# Update all nuspec files
$nuspecFiles = @(
    "src\Chet.QuartzNet.UI\Chet.QuartzNet.UI.nuspec",
    "src\Chet.QuartzNet.EFCore.MySql\Chet.QuartzNet.EFCore.MySQL.nuspec",
    "src\Chet.QuartzNet.EFCore.PostgreSql\Chet.QuartzNet.EFCore.PostgreSQL.nuspec",
    "src\Chet.QuartzNet.EFCore.SQLite\Chet.QuartzNet.EFCore.SQLite.nuspec",
    "src\Chet.QuartzNet.EFCore.SqlServer\Chet.QuartzNet.EFCore.SqlServer.nuspec"
)

foreach ($nuspecFile in $nuspecFiles) {
    if (Test-Path -Path $nuspecFile) {
        $content = Get-Content -Path $nuspecFile -Raw -Encoding UTF8
        $content = $content -replace '<version>(.*?)</version>', "<version>$version</version>"
        $content = $content -replace '<releaseNotes>(.*?)</releaseNotes>', "<releaseNotes>https://github.com/qiect/Chet.QuartzNet.UI/releases/tag/${version}</releaseNotes>"
        Set-Content -Path $nuspecFile -Value $content -Encoding UTF8
        Write-Host "Updated: $nuspecFile"
    } else {
        Write-Host "Warning: $nuspecFile not found, skipping."
    }
}

Write-Host "Version synchronization completed!"