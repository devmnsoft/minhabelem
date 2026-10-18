param([Parameter(Mandatory=$true)][string]$ReportPath)
$ErrorActionPreference='Stop'
$allowedRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'MNSOFT\Homologacao'))+[IO.Path]::DirectorySeparatorChar
$resolved=[IO.Path]::GetFullPath($ReportPath)
if(-not $resolved.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Use somente o relatório local criado pelo bootstrap ou pela recuperação autorizada.'}
$report=Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
if([datetime]$report.ExpiraEmUtc -lt [datetime]::UtcNow){Remove-Item -LiteralPath $resolved;throw 'Relatório expirado e removido.'}
Add-Type -AssemblyName System.Windows.Forms
$form=New-Object Windows.Forms.Form
$form.Text='MinhaBelém — credenciais locais de homologação (leitura única)'
$form.Width=1100;$form.Height=420;$form.StartPosition='CenterScreen'
$grid=New-Object Windows.Forms.DataGridView
$grid.Dock='Fill';$grid.ReadOnly=$true;$grid.AllowUserToAddRows=$false;$grid.AutoSizeColumnsMode='AllCells'
$table=New-Object System.Data.DataTable
foreach($column in @('Login','Perfil','SenhaTemporaria','Situacao','MFAObrigatorio','TrocaSenhaObrigatoria','AutenticacaoPersistida')){[void]$table.Columns.Add($column)}
foreach($account in $report.Contas){$row=$table.NewRow();foreach($column in $table.Columns){$row[$column.ColumnName]=[string]$account.($column.ColumnName)};$table.Rows.Add($row)}
$grid.DataSource=$table;$form.Controls.Add($grid)
# Consume the restricted temporary file before showing it. Credentials are never written to stdout.
Remove-Item -LiteralPath $resolved
[void]$form.ShowDialog()
$table.Clear();$form.Dispose();$report=$null
