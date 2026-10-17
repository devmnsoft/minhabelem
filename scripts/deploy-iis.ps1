<#
.SYNOPSIS
    Script de publicação assistida com validação de saúde e rollback automático para IIS.
.DESCRIPTION
    Executa:
    1. Pre-flight check (build Release e testes obrigatórios).
    2. Publicação para diretório temporário (Staging).
    3. Backup integral da versão anterior.
    4. Atualização atômica dos binários no diretório de destino do IIS.
    5. Reciclagem e reinício do Application Pool.
    6. Validação do endpoint de Health Check (/health).
    7. Rollback automático em caso de falha de inicialização.
#>
param(
    [string]$SolutionPath = ".\MinhaBelem.sln",
    [string]$ProjectPath = ".\src\MinhaBelem.Web\MinhaBelem.Web.csproj",
    [string]$TargetDir = "C:\inetpub\minhabelem",
    [string]$BackupBaseDir = "C:\inetpub\backups",
    [string]$AppPoolName = "MinhaBelemPool",
    [string]$HealthCheckUrl = "http://localhost/health",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " MINHABELÉM — DEPLOY EM PRODUÇÃO / IIS COM ROLLBACK" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Validação de Ferramental
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "O .NET SDK 8.0 não foi localizado no PATH do sistema."
    exit 1
}

# 2. Verificação Prévia de Compilação e Testes
Write-Host "[1/6] Executando Build Release e validação de testes..." -ForegroundColor Cyan
dotnet build $SolutionPath -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Falha na compilação Release. Operação de deploy cancelada."
    exit 1
}

if (-not $SkipTests) {
    Write-Host "      Executando testes automatizados..." -ForegroundColor Gray
    dotnet test $SolutionPath -c Release --no-build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Testes automatizados falharam. Não é permitido implantar versão não homologada!"
        exit 1
    }
}

# 3. Publicação em Diretório de Staging
$stagingDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "MinhaBelem_Staging_" + [System.Guid]::NewGuid().ToString("N"))
Write-Host "[2/6] Gerando publicação Release em staging: $stagingDir..." -ForegroundColor Cyan
dotnet publish $ProjectPath -c Release -r win-x64 --self-contained false -o $stagingDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Falha no dotnet publish."
    exit 1
}

# 4. Backup da Versão Atual
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir = Join-Path $BackupBaseDir "minhabelem_$timestamp"
if (Test-Path $TargetDir) {
    Write-Host "[3/6] Criando backup da versão atual em: $backupDir..." -ForegroundColor Cyan
    if (-not (Test-Path $BackupBaseDir)) { New-Item -Path $BackupBaseDir -ItemType Directory -Force | Out-Null }
    Copy-Item -Path $TargetDir -Destination $backupDir -Recurse -Force
    Write-Host "[✓] Backup concluído com sucesso." -ForegroundColor Green
}

# 5. Aplicação dos Novos Binários no IIS
Write-Host "[4/6] Atualizando binários em $TargetDir..." -ForegroundColor Cyan
try {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    if (Get-Module -Name WebAdministration) {
        # Coloca o arquivo app_offline.htm temporário para evitar travamento de DLLs
        $appOffline = Join-Path $TargetDir "app_offline.htm"
        Set-Content -Path $appOffline -Value "<html><body style='font-family:sans-serif;text-align:center;padding:50px;'><h2>MinhaBelém em atualização...</h2><p>Voltaremos em alguns instantes.</p></body></html>"
        Start-Sleep -Seconds 2
    }

    Copy-Item -Path "$stagingDir\*" -Destination $TargetDir -Recurse -Force

    # Remove o arquivo app_offline.htm
    $appOffline = Join-Path $TargetDir "app_offline.htm"
    if (Test-Path $appOffline) { Remove-Item $appOffline -Force }

    # Recicla o AppPool
    if (Get-Module -Name WebAdministration -and (Test-Path "IIS:\AppPools\$AppPoolName")) {
        Restart-WebAppPool -Name $AppPoolName
        Write-Host "[✓] Application Pool '$AppPoolName' reciclado." -ForegroundColor Green
    }
}
catch {
    Write-Error "Erro durante cópia dos arquivos: $_"
    if (Test-Path $backupDir) {
        Write-Warning "Iniciando rollback imediato..."
        Copy-Item -Path "$backupDir\*" -Destination $TargetDir -Recurse -Force
        if (Test-Path "IIS:\AppPools\$AppPoolName") { Restart-WebAppPool -Name $AppPoolName }
        Write-Host "[!] Rollback para versão anterior concluído." -ForegroundColor Yellow
    }
    exit 1
}
finally {
    # Limpa diretório de staging
    if (Test-Path $stagingDir) { Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction SilentlyContinue }
}

# 6. Validação do Health Check Pós-Deploy
Write-Host "[5/6] Verificando integridade da aplicação ($HealthCheckUrl)..." -ForegroundColor Cyan
$healthy = $false
for ($i = 1; $i -le 10; $i++) {
    Start-Sleep -Seconds 2
    try {
        $response = Invoke-WebRequest -Uri $HealthCheckUrl -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            $healthy = $true
            break
        }
    }
    catch {
        Write-Host "      Aguardando inicialização da aplicação (tentativa $i/10)..." -ForegroundColor Gray
    }
}

if ($healthy) {
    Write-Host "[✓] Aplicação online e saudável (Health Check HTTP 200 OK)." -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host " DEPLOY CONCLUÍDO COM SUCESSO! VERSÃO EM OPERAÇÃO." -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
} else {
    Write-Error "[!] Falha no Health Check após o deploy! Iniciando ROLLBACK automático..."
    if (Test-Path $backupDir) {
        Copy-Item -Path "$backupDir\*" -Destination $TargetDir -Recurse -Force
        if (Test-Path "IIS:\AppPools\$AppPoolName") { Restart-WebAppPool -Name $AppPoolName }
        Write-Host "[!] Rollback executado. A versão anterior foi restaurada com sucesso." -ForegroundColor Yellow
    } else {
        Write-Error "[!] Diretório de backup não encontrado para rollback."
    }
    exit 1
}
