param([switch]$IncludeServices,[switch]$BuildImage)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function CheckExit([string]$message){if($LASTEXITCODE -ne 0){throw $message}}
Push-Location $root
$oldMcpExport=$env:VAIRAS_MCP_EXPORT_CONTRACTS; $oldChatExport=$env:VAIRAS_CHAT_EXPORT_CONTRACTS
try {
    $run=Join-Path $root ('evidence/runs/'+(Get-Date -Format 'yyyyMMdd-HHmmss')); New-Item -ItemType Directory -Force $run | Out-Null
    @{status='RUNNING';run=$run;checkedAt=(Get-Date).ToUniversalTime().ToString('o')} | ConvertTo-Json | Set-Content -Encoding UTF8 evidence/verification-summary.json
    if(-not(Test-Path evidence/image-smoke.json)){@{status='NOT RUN'} | ConvertTo-Json | Set-Content -Encoding UTF8 evidence/image-smoke.json}
    py -3 scripts/check-artifacts.py; CheckExit 'Artifact check failed.'
    dotnet restore Vairas.McpServer.slnx; CheckExit 'MCP restore/audit failed.'
    dotnet build Vairas.McpServer.slnx -c Release --no-restore; CheckExit 'MCP Release build failed.'
    dotnet test Vairas.McpServer.Tests -c Release --no-build --no-restore --filter Category=Unit --settings coverage.runsettings --collect:'XPlat Code Coverage' --logger 'trx;LogFileName=unit.trx' --results-directory (Join-Path $run 'mcp-unit')
    CheckExit 'MCP unit tests failed.'
    $file=Get-ChildItem (Join-Path $run 'mcp-unit') -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
    if(-not $file){throw 'Fresh unit coverage missing.'}; [xml]$report=Get-Content -Raw $file.FullName; $rates=@{}
    foreach($name in @('Vairas.McpServer.Business','Vairas.McpServer')){
        $package=$report.coverage.packages.package | Where-Object {$_.name -eq $name}
        if(-not $package){throw "Coverage package missing: $name"};$rate=[Math]::Round(100*[double]$package.'line-rate',2)
        if($rate -lt 70){throw "Unit coverage below 70%: ${name}: $rate"};$rates[$name]=$rate
    }
    $env:VAIRAS_MCP_EXPORT_CONTRACTS='on'
    dotnet test Vairas.McpServer.Tests -c Release --no-build --no-restore --filter Category=Http --logger 'trx;LogFileName=http.trx' --results-directory (Join-Path $run 'mcp-http')
    CheckExit 'MCP SDK HTTP tests failed.'
    foreach($owner in @('Authentication','UserManagement','SchoolManagement','Scheduling','LearningTracking','Gateway','ChatBackend')){
        $project="../Vairas.$owner/Vairas.$owner.Tests/Vairas.$owner.Tests.csproj"
        dotnet restore $project; CheckExit "Restore failed: $owner"
        if($owner -in @('Gateway','ChatBackend')){
            dotnet test $project -c Release --no-restore --logger 'trx;LogFileName=regression.trx' --results-directory (Join-Path $run $owner)
        } else {
            dotnet test $project -c Release --no-restore --filter 'TestCategory=Unit|Category=Unit' --logger 'trx;LogFileName=regression.trx' --results-directory (Join-Path $run $owner)
        }
        CheckExit "Regression failed: $owner"
    }
    $services='NOT RUN';$image='NOT RUN'
    if($IncludeServices){
        $env:VAIRAS_CHAT_EXPORT_CONTRACTS='on'
        dotnet restore ../Vairas.ChatBackend/Vairas.ChatBackend.ServiceTests; CheckExit 'Service smoke restore failed.'
        dotnet test ../Vairas.ChatBackend/Vairas.ChatBackend.ServiceTests -c Release --no-restore --logger 'trx;LogFileName=services.trx' --results-directory (Join-Path $run 'services')
        CheckExit 'Six-owner MCP smoke failed.';$services='PASSED'
    }
    py -3 scripts/check-artifacts.py; CheckExit 'Contract export drift detected; review and regenerate intentional source changes.'
    node ../Vairas.Gateway/scripts/check-contracts.cjs; CheckExit 'Gateway catalogue drift.'
    if($BuildImage){
        docker build -t vairas-mcp:local .; CheckExit 'MCP image build failed.'
        & (Join-Path $PSScriptRoot 'verify-image.ps1'); $image='PASSED'
        docker build -t vairas-chat:local ../Vairas.ChatBackend; CheckExit 'Chat image regression failed.'
        & (Join-Path $root '../Vairas.ChatBackend/scripts/verify-image.ps1')
        docker build -t vairas-gateway:local ../Vairas.Gateway; CheckExit 'Gateway image regression failed.'
        & (Join-Path $root '../Vairas.Gateway/scripts/verify-image.ps1')
    }
    $audit=dotnet list Vairas.McpServer.slnx package --vulnerable --include-transitive --format json; CheckExit 'Dependency audit failed.'
    $audit | Set-Content -Encoding UTF8 (Join-Path $run 'dependency-audit.json')
    $data=($audit -join "`n") | ConvertFrom-Json
    foreach($project in $data.projects){foreach($framework in $project.frameworks){foreach($package in @($framework.topLevelPackages)+@($framework.transitivePackages)){if($package.vulnerabilities){throw 'Vulnerable dependency detected.'}}}}
    @{checkedAt=(Get-Date).ToUniversalTime().ToString('o');run=$run;unitLineCoverage=$rates;services=$services;image=$image;liveCloud='NOT RUN';businessDispatch='DISABLED';productionDeployment='NOT RUN'} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 evidence/verification-summary.json
    py -3 scripts/summarize.py; CheckExit 'Summary derivation failed.'
} catch {
    @{checkedAt=(Get-Date).ToUniversalTime().ToString('o');run=$run;status='FAILED';note='Inspect the fresh TRX and console output.'} | ConvertTo-Json | Set-Content -Encoding UTF8 evidence/verification-summary.json
    throw
} finally {$env:VAIRAS_MCP_EXPORT_CONTRACTS=$oldMcpExport;$env:VAIRAS_CHAT_EXPORT_CONTRACTS=$oldChatExport;Pop-Location}
