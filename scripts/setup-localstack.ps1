param(
    [string]$Bucket = "cfapi-files",
    [string]$Image = "localstack/localstack:3"
)

docker rm -f localstack 2>$null | Out-Null
docker run -d --name localstack -p 4566:4566 -e SERVICES=s3 $Image | Out-Null

for ($i = 0; $i -lt 20; $i++) {
    try {
        Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:4566/_localstack/health" -TimeoutSec 2 | Out-Null
        break
    }
    catch {
        Start-Sleep -Seconds 1
    }
}

$null = aws --endpoint-url=http://localhost:4566 s3api head-bucket --bucket $Bucket 2>$null
if ($LASTEXITCODE -ne 0) {
    aws --endpoint-url=http://localhost:4566 s3 mb "s3://$Bucket"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create bucket '$Bucket'. Check docker logs localstack."
    }
}

Write-Host "LocalStack ready, bucket '$Bucket' is available."
