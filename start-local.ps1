$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($dotnetCmd -and (& $dotnetCmd.Source --list-sdks | Select-String '^10\.')) { $dotnetCmd.Source } else { Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet10\dotnet.exe' }
if (-not (Test-Path $dotnet)) { throw '.NET SDK 10 belum tersedia. Pasang dari https://dotnet.microsoft.com/download/dotnet/10.0' }

if (-not (Test-Path '.env')) {
    $dbPassword = 'Pg' + [guid]::NewGuid().ToString('N')
    $demoPassword = 'Rescue!' + [guid]::NewGuid().ToString('N').Substring(0, 12) + 'aA1'
    @("POSTGRES_PASSWORD=$dbPassword", "RESCUE_DEMO_PASSWORD=$demoPassword") | Set-Content -LiteralPath '.env'
    Write-Host 'Kredensial lokal baru dibuat di .env (tidak masuk Git).'
}

Get-Content -LiteralPath '.env' | ForEach-Object {
    if ($_ -match '^([A-Z_]+)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1], $matches[2], 'Process') }
}
if (-not $env:POSTGRES_PASSWORD -or -not $env:RESCUE_DEMO_PASSWORD) { throw '.env harus berisi POSTGRES_PASSWORD dan RESCUE_DEMO_PASSWORD.' }
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5433;Database=rescue_checklist;Username=rescue;Password=$env:POSTGRES_PASSWORD"
$env:ASPNETCORE_ENVIRONMENT = 'Development'

docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) {
    $dockerDesktop = 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
    if (Test-Path $dockerDesktop) { Start-Process -FilePath $dockerDesktop -WindowStyle Hidden }
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
    }
}
docker compose up -d
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose gagal dijalankan.' }
for ($i = 0; $i -lt 30; $i++) {
    docker compose exec -T postgres pg_isready -U rescue -d rescue_checklist 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { break }
    Start-Sleep -Seconds 2
}
Write-Host ''
Write-Host 'Aplikasi: http://localhost:5137'
Write-Host 'Akun demo: admin, pic, rescue, management'
Write-Host "Kata sandi demo: $env:RESCUE_DEMO_PASSWORD"
Write-Host 'Tekan Ctrl+C untuk menghentikan web. Database tetap berjalan.'
& $dotnet run --project src/RescueChecklist.Web --urls http://localhost:5137
