param([string]$Image='vairas-mcp:local')
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$name='mcp-check-'+[Guid]::NewGuid().ToString('N'); $container=''
try {
    # An isolated synthetic secret satisfies startup validation; no owner or cloud is contacted.
    $secret=[Guid]::NewGuid().ToString('N')
    $container=(docker run --rm -d --name $name -p '127.0.0.1::8080' -e "Dependencies__ClientSecret=$secret" $Image).Trim()
    if($LASTEXITCODE -ne 0 -or $container -notmatch '^[a-f0-9]{64}$'){throw 'MCP image did not start.'}
    $endpoint=(docker port $container 8080/tcp).Trim()
    if($endpoint -notmatch '^127\.0\.0\.1:\d+$'){throw 'Expected loopback-only smoke exposure.'}
    $ready=$false
    for($attempt=0;$attempt -lt 60;$attempt++){
        try { if((Invoke-WebRequest -UseBasicParsing "http://$endpoint/health/ready" -Headers @{Host='localhost'} -TimeoutSec 2).StatusCode -eq 200){$ready=$true;break} } catch { }
        Start-Sleep -Milliseconds 200
    }
    if(-not $ready){throw 'MCP configuration readiness failed.'}
    $user=(docker inspect --format '{{.Config.User}}' $container).Trim()
    if($user -in @('','0','root')){throw 'Nonroot runtime required.'}
    try { Invoke-WebRequest -UseBasicParsing "http://$endpoint/mcp" -Method Post -ContentType 'application/json' -Body '{}' -Headers @{Host='localhost';'X-Forwarded-Proto'='https'} -TimeoutSec 3 | Out-Null; throw 'Untrusted cleartext MCP accepted.' }
    catch { if(-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 403){throw} }
    @{checkedAt=(Get-Date).ToUniversalTime().ToString('o');image=$Image;imageId=(docker inspect --format '{{.Image}}' $container).Trim();nonRootUser=$user;readiness=200;untrustedCleartext=403;ownerConnectivity='NOT RUN';liveCloud='NOT RUN'} | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $root 'evidence/image-smoke.json')
    Get-Content (Join-Path $root 'evidence/image-smoke.json')
} finally { if($container -match '^[a-f0-9]{64}$'){docker rm -f $container | Out-Null} }
