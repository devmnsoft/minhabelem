param(
 [string]$Project = ".\src\MinhaBelem.Web\MinhaBelem.Web.csproj",
 [string]$Output = "C:\Publicacao\MinhaBelem"
)
$ErrorActionPreference = "Stop"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "Instale o .NET SDK 8 antes de executar." }
dotnet restore $Project
if ($LASTEXITCODE -ne 0) { throw "Falha no restore" }
dotnet build $Project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Falha no build" }
dotnet publish $Project -c Release -r win-x64 --self-contained false -o $Output
if ($LASTEXITCODE -ne 0) { throw "Falha na publicacao" }
Write-Host "Publicacao gerada em: $Output"
Write-Warning "Antes de implantar: executar MINHABELEM.SQL na base MySQL selecionada e testar integracoes."
