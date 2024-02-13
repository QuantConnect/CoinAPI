#!/bin/bash

processing_date="${QC_DATAFLEET_DEPLOYMENT_DATE}"
processing_date_yesterday="$(date -d "${processing_date} -1 days" +%Y%m%d)"
market="*"
include="*"
filter="_SPOT*"

if [ -n "$2" ]; then
    filter="${2}*"
fi

if [ -n "$1" ]; then
    market=$(echo "${1}" | tr [a-z] [A-Z])
    include="*${market}${filter}"
    echo "Downloading crypto TAQ for market: ${market} include ${include}"
else
    echo "Downloading crypto TAQ for all markets"
fi

# We store AWS creds and set the CoinAPI creds instead
aws_access_key_id="${AWS_ACCESS_KEY_ID}"
aws_secret_access_key="${AWS_SECRET_ACCESS_KEY}"

export AWS_ACCESS_KEY_ID="${COINAPI_AWS_ACCESS_KEY_ID}"
export AWS_SECRET_ACCESS_KEY="${COINAPI_AWS_SECRET_ACCESS_KEY}"

# multipart corrupts files
aws configure set default.s3.multipart_threshold 100GB

# We sync yesterdays file too because midnight data of 'processing_date' might be in the end of yesterdays file
aws --endpoint-url=http://flatfiles.coinapi.io s3 sync s3://coinapi/trades/$processing_date_yesterday/$market/ /raw/crypto/coinapi/trades/$processing_date_yesterday/ --exclude='*' --include="${include}"
aws --endpoint-url=http://flatfiles.coinapi.io s3 sync s3://coinapi/quotes/$processing_date_yesterday/$market/ /raw/crypto/coinapi/quotes/$processing_date_yesterday/ --exclude='*' --include="${include}"

aws --endpoint-url=http://flatfiles.coinapi.io s3 sync s3://coinapi/trades/$processing_date/$market/ /raw/crypto/coinapi/trades/$processing_date/ --exclude='*' --include="${include}"
aws --endpoint-url=http://flatfiles.coinapi.io s3 sync s3://coinapi/quotes/$processing_date/$market/ /raw/crypto/coinapi/quotes/$processing_date/ --exclude='*' --include="${include}"

# Restore AWS creds
export AWS_ACCESS_KEY_ID="$aws_access_key_id"
export AWS_SECRET_ACCESS_KEY="$aws_secret_access_key"