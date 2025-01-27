#!/bin/sh
set -e

export AWS_ACCESS_KEY_ID=key
export AWS_SECRET_ACCESS_KEY=secret
export AWS_DEFAULT_REGION=us-east-1


# Create DynamoDB table for file's metadata
aws --endpoint-url=http://localhost:4566 --region us-east-1 dynamodb create-table \
    --table-name Files \
    --attribute-definitions \
        AttributeName=Filename,AttributeType=S \
        AttributeName=UploadedAt,AttributeType=S \
        AttributeName=FileHash,AttributeType=S \
    --key-schema AttributeName=Filename,KeyType=HASH AttributeName=UploadedAt,KeyType=RANGE \
    --global-secondary-indexes \
        "[
            {
                \"IndexName\":\"FileHashIndex\",
                \"KeySchema\":[{\"AttributeName\":\"FileHash\",\"KeyType\":\"HASH\"},
                                {\"AttributeName\":\"UploadedAt\",\"KeyType\":\"RANGE\"}],
                \"Projection\":{\"ProjectionType\":\"ALL\"},
                 \"ProvisionedThroughput\": {
                    \"ReadCapacityUnits\": 5,
                    \"WriteCapacityUnits\": 5
                }
            }
        ]" \
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5   


# Create S3 bucket
aws --endpoint-url=http://localhost:4566 --region us-east-1 s3 mb s3://storage
