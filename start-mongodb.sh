#!/bin/bash

echo "Starting MongoDB replica set..."
docker-compose up -d

echo "Waiting for MongoDB instances to start..."
sleep 10

echo "Initializing replica set..."
docker exec mongo1 /scripts/rs-init.sh

echo "MongoDB replica set is ready!" 