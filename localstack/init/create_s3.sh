#!/bin/sh
set -e

echo "Running LocalStack init script to create S3 bucket and object"

# Wait for awslocal to be available
for i in $(seq 1 30); do
  if command -v awslocal >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

# Create bucket and upload file (ignore errors if already exists)
awslocal s3 mb s3://test-bucket || true
awslocal s3 cp /etc/localstack/init/ready.d/test-file.txt s3://test-bucket/test-file.txt || true

echo "LocalStack S3 initialization complete" 
