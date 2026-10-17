<#
.SYNOPSIS
    Gerenciador do Serviço do Windows MinhaBelem.Worker (Radar e Coleta Autônoma).
.DESCRIPTION
    Permite:
    - Publicar os binários do Worker.
    - Instalar como Serviço do Windows com inicialização automática e recuperação de falhas.
    - Iniciar, parar, reiniciar e verificar status do serviço.
    - Desinstalar o serviço.
    - Executar um ciclo de teste pontual (--run-once).
#>
param(
    [ValidateSet('publish', 'install', 'start', 'stop', 'restart', 'status', 'uninstall', 'test-once')]
    [string]$Action = 'status',
    [string]$ServiceName = "MinhaBelemWorker",
    [string]$ServiceDisplayName = "MinhaBelem - Radar e Coleta Autonoma",
    [string]$ServiceDescription = "Servico autonomo de monitoramento de fontes publicas, extracao de eventos e triagem editorial para o portal MinhaBelem.",
    [string]$TargetDir = "C:\Services\MinhaBelemWorker",
    [string]$WorkerProject = ".\src\MinhaBelem.Worker\MinhaBelem.Worker.csproj"
)

$ErrorActionPreference = "Stop"

$exePath = Join-Path $TargetDir "MinhaBelem.Worker.exe"

switch ($Action) {
    'publish' {
        Write-Host "[+] Publicando MinhaBelem.Worker Release em: $TargetDir..." -ForegroundColor Cyan
        if (-not (Test-Path $TargetDir)) { New-Item -Path $TargetDir -ItemType Directory -Force | Out-Null }
        dotnet publish $WorkerProject -c Release -r win-x64 --self-contained false -o $TargetDir
        Write-Host "[✓] Publicação do Worker concluída." -ForegroundColor Green
    }

    'install' {
        if (-not (Test-Path $exePath)) {
            Write-Host "[!] Executável não encontrado em $exePath. Publicando automaticamente..." -ForegroundColor Yellow
            & $PSCommandPath -Action publish -TargetDir $TargetDir -WorkerProject $WorkerProject
        }

        Write-Host "[+] Registrando serviço do Windows '$ServiceName'..." -ForegroundColor Cyan
        $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($existing) {
            Write-Warning "O serviço '$ServiceName' já está instalado."
            return
        }

        # Criação do serviço no Windows
        New-Service -Name $ServiceName `
            -BinaryPathName "`"$exePath`"" `
            -DisplayName $ServiceDisplayName `
            -Description $ServiceDescription `
            -StartupType Automatic

        # Configuração de reinício automático após falha via sc.exe (recuperação)
        sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

        Write-Host "[✓] Serviço '$ServiceName' instalado com sucesso (Inicialização Automática e Recuperação de Falhas ativadas)." -ForegroundColor Green
        Write-Host "    Para iniciar execute: .\scripts\manage-worker-service.ps1 -Action start" -ForegroundColor Cyan
    }

    'start' {
        Write-Host "[+] Iniciando serviço '$ServiceName'..." -ForegroundColor Cyan
        Start-Service -Name $ServiceName
        Start-Sleep -Seconds 2
        Get-Service -Name $ServiceName | Format-Table -AutoSize
    }

    'stop' {
        Write-Host "[+] Parando serviço '$ServiceName'..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 1
        Get-Service -Name $ServiceName | Format-Table -AutoSize
    }

    'restart' {
        Write-Host "[+] Reiniciando serviço '$ServiceName'..." -ForegroundColor Cyan
        Restart-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
        Get-Service -Name $ServiceName | Format-Table -AutoSize
    }

    'status' {
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($svc) {
            Write-Host "[i] Informações do Serviço '$ServiceName':" -ForegroundColor Cyan
            $svc | Format-Table -AutoSize
        } else {
            Write-Warning "O serviço '$ServiceName' não está instalado neste servidor."
            Write-Host "    Para instalar: .\scripts\manage-worker-service.ps1 -Action install" -ForegroundColor Gray
        }
    }

    'uninstall' {
        Write-Host "[!] Desinstalando serviço '$ServiceName'..." -ForegroundColor Yellow
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($svc) {
            if ($svc.Status -eq 'Running') { Stop-Service -Name $ServiceName -Force }
            sc.exe delete $ServiceName | Out-Null
            Write-Host "[✓] Serviço '$ServiceName' removido do Windows." -ForegroundColor Green
        } else {
            Write-Warning "O serviço '$ServiceName' não foi encontrado."
        }
    }

    'test-once' {
        Write-Host "[+] Executando ciclo único de teste (--run-once)..." -ForegroundColor Cyan
        if (Test-Path $exePath) {
            & $exePath --run-once
        } else {
            dotnet run --project $WorkerProject -c Release -- --run-once
        }
    }
}
