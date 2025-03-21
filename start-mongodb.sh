#!/bin/bash

echo "Starting MongoDB replica set..."
docker-compose up -d mongo1 mongo2 mongo3

echo "Waiting for MongoDB instances to be ready..."
# Wait for MongoDB server health check
until docker ps | grep mongo1 | grep -q "(healthy)"
do
  echo "Waiting for MongoDB to be healthy..."
  sleep 5
done

echo "Initializing replica set..."
docker exec mongo1 /scripts/rs-init.sh

echo "Verifying MongoDB replica set..."
# Check if a PRIMARY is available
ATTEMPTS=0
MAX_ATTEMPTS=30
DELAY=5

while [ $ATTEMPTS -lt $MAX_ATTEMPTS ]; do
  PRIMARY=$(docker exec mongo1 mongosh --quiet --eval "rs.isMaster().primary" | tr -d '\r')
  if [ -n "$PRIMARY" ] && [ "$PRIMARY" != "null" ]; then
    echo "Primary node available: $PRIMARY"
    echo "MongoDB replica set is ready!"
    
    echo "Starting the sample API..."
    docker-compose up -d sample-api
    
    echo "All services are up and running!"
    exit 0
  fi
  
  ATTEMPTS=$((ATTEMPTS+1))
  echo "Waiting for PRIMARY node... (Attempt $ATTEMPTS/$MAX_ATTEMPTS)"
  sleep $DELAY
done

echo "ERROR: Failed to detect a PRIMARY node in the replica set after $MAX_ATTEMPTS attempts!"
exit 1 