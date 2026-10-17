<#
.SYNOPSIS
    Script de preparação do ambiente Windows Server e IIS para o MinhaBelém.
.DESCRIPTION
    Verifica e prepara:
    - Funções do IIS (AspNetCoreModuleV2).
    - Application Pool dedicado com 'No Managed Code'.
    - Diretórios da aplicação com permissões NTFS seguras (princípio do privilégio mínimo).
    - Diretório de chaves do ASP.NET Data Protection com ACL restrita.
    - Diretório do MinhaBelem.Worker para execução como Serviço do Windows.
#>
param(
    [string]$AppPoolName = "MinhaBelemPool",
    [string]$SitePath = "C:\inetpub\minhabelem",
    [string]$DataProtectionPath = "C:\MinhaBelemDataProtection",
    [string]$WorkerPath = "C:\Services\MinhaBelemWorker",
    [switch]$SkipElevatedCheck
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " MINHABELÉM — PREPARAÇÃO DO WINDOWS SERVER / IIS (SPRINT 12)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (-not $SkipElevatedCheck) {
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Error "Este script deve ser executado como Administrador no Windows Server."
        exit 1
    }
}

# 1. Verificação do Sistema Operacional
$os = Get-CimInstance Win32_OperatingSystem
Write-Host "[✓] Sistema Operacional: $($os.Caption) (Build $($os.BuildNumber))" -ForegroundColor Green

# 2. Verificação do IIS e Módulo ASP.NET Core
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (-not (Get-Module -Name WebAdministration)) {
    Write-Warning "Módulo WebAdministration não encontrado. Instalando IIS Web-Server..."
    Install-WindowsFeature -Name Web-Server, Web-Asp-Net45, Web-Http-Errors, Web-Static-Content -IncludeManagementTools
}

$ancm = Get-WebGlobalModule | Where-Object { $_.Name -like "*AspNetCoreModule*" }
if ($ancm) {
    Write-Host "[✓] ASP.NET Core Hosting Bundle detectado: $($ancm.Name)" -ForegroundColor Green
} else {
    Write-Warning "[!] AspNetCoreModuleV2 não foi detectado no IIS. Certifique-se de instalar o ASP.NET Core 8 Hosting Bundle!"
    Write-Host "    Download oficial: https://dotnet.microsoft.com/permalink/dotnet/8.0/aspnetcore-runtime-win-x64.exe" -ForegroundColor Yellow
}

# 3. Criação de Diretórios com Permissões Restritas
$dirs = @($SitePath, $DataProtectionPath, $WorkerPath, "$SitePath\logs", "C:\inetpub\backups")
foreach ($dir in $dirs) {
    if (-not (Test-Path $dir)) {
        New-Item -Path $dir -ItemType Directory -Force | Out-Null
        Write-Host "[✓] Criado diretório: $dir" -ForegroundColor Green
    } else {
        Write-Host "[i] Diretório existente: $dir" -ForegroundColor Gray
    }
}

# 4. Configuração do Application Pool no IIS
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    Write-Host "[+] Criando Application Pool '$AppPoolName'..." -ForegroundColor Cyan
    New-Item "IIS:\AppPools\$AppPoolName" | Out-Null
}

Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "processModel.idleTimeout" -Value ([TimeSpan]::Zero)
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "startMode" -Value "AlwaysRunning"
Write-Host "[✓] Application Pool '$AppPoolName' configurado (No Managed Code, AlwaysRunning)." -ForegroundColor Green

# 5. Configuração de Permissões NTFS (ACLs)
$appPoolUser = "IIS AppPool\$AppPoolName"

# Permissão no Site Web (Leitura e Execução + Gravação apenas em logs)
icacls $SitePath /grant "$($appPoolUser):(OI)(CI)RX" /t /c /q
icacls "$SitePath\logs" /grant "$($appPoolUser):(OI)(CI)F" /t /c /q

# Permissão exclusiva para chaves Data Protection (Evita vazamento de tokens e cookies entre pools)
icacls $DataProtectionPath /inheritance:r /c /q
icacls $DataProtectionPath /grant "SYSTEM:(OI)(CI)F" /grant "Administrators:(OI)(CI)F" /grant "$($appPoolUser):(OI)(CI)F" /c /q
Write-Host "[✓] Permissões NTFS configuradas com segurança para '$appPoolUser' e chaves criptográficas." -ForegroundColor Green

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " PRÉ-REQUISITOS CONFIGURADOS COM SUCESSO!" -ForegroundColor Green
Write-Host " Próximos passos:" -ForegroundColor Cyan
Write-Host " 1. Executar .\scripts\deploy-iis.ps1 para publicar o portal Web."
Write-Host " 2. Executar .\scripts\manage-worker-service.ps1 -Action install para ativar o Radar autônomo."
Write-Host " 3. Vincular o certificado SSL/TLS no IIS para minhabelem.com.br (Porta 443)."
Write-Host "==========================================================" -ForegroundColor Cyan
