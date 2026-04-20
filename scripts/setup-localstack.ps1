param(
    [string]$Bucket = "cfapi-files"
)

docker rm -f localstack 2>$null | Out-Null
docker run -d --name localstack -p 4566:4566 -e SERVICES=s3 localstack/localstack

Start-Sleep -Seconds 5
aws --endpoint-url=http://localhost:4566 s3 mb "s3://$Bucket"

Write-Host "LocalStack ready, bucket '$Bucket' created."
