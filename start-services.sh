#!/bin/bash

set -e  # Exit on error

# Color variables
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}===== Starting MongoDB Replica Set and Application Services =====${NC}"

# Stop any running containers and remove volumes
echo -e "${YELLOW}Stopping any existing containers and cleaning volumes...${NC}"
docker-compose down -v

# Start fresh
echo -e "${GREEN}Starting MongoDB replica set nodes...${NC}"
docker-compose build
docker-compose up -d mongo1 mongo2 mongo3

# Wait for MongoDB servers to be healthy
echo -e "${YELLOW}Waiting for MongoDB nodes to be healthy...${NC}"
MAX_ATTEMPTS=30
DELAY=5

for i in {1..30}; do
  MONGO1_STATUS=$(docker inspect --format='{{.State.Health.Status}}' mongo1 2>/dev/null || echo "not_running")
  MONGO2_STATUS=$(docker inspect --format='{{.State.Health.Status}}' mongo2 2>/dev/null || echo "not_running")
  MONGO3_STATUS=$(docker inspect --format='{{.State.Health.Status}}' mongo3 2>/dev/null || echo "not_running")
  
  if [ "$MONGO1_STATUS" == "healthy" ] && [ "$MONGO2_STATUS" == "healthy" ] && [ "$MONGO3_STATUS" == "healthy" ]; then
    echo -e "${GREEN}All MongoDB nodes are healthy!${NC}"
    break
  fi
  
  echo -e "${YELLOW}Waiting for MongoDB nodes to be healthy (Attempt $i/$MAX_ATTEMPTS)...${NC}"
  echo -e "  mongo1: $MONGO1_STATUS"
  echo -e "  mongo2: $MONGO2_STATUS"
  echo -e "  mongo3: $MONGO3_STATUS"
  sleep $DELAY
  
  if [ $i -eq $MAX_ATTEMPTS ]; then
    echo -e "${RED}MongoDB nodes did not become healthy within the allowed time.${NC}"
    exit 1
  fi
done

# Initialize the replica set
echo -e "${GREEN}Initializing MongoDB replica set...${NC}"
docker-compose up -d mongo-init

# Wait for replica set initialization to complete
echo -e "${YELLOW}Waiting for mongo-init service to complete...${NC}"
for i in {1..30}; do
  INIT_STATUS=$(docker inspect --format='{{.State.Status}}' mongo-init 2>/dev/null || echo "not_created")
  
  if [ "$INIT_STATUS" == "exited" ]; then
    EXIT_CODE=$(docker inspect --format='{{.State.ExitCode}}' mongo-init)
    if [ "$EXIT_CODE" == "0" ]; then
      echo -e "${GREEN}Replica set initialization completed successfully.${NC}"
      break
    else
      echo -e "${RED}Replica set initialization failed with exit code $EXIT_CODE.${NC}"
      docker logs mongo-init
      exit 1
    fi
  fi
  
  echo -e "${YELLOW}Waiting for replica set initialization to complete (Attempt $i/$MAX_ATTEMPTS)...${NC}"
  sleep $DELAY
  
  if [ $i -eq $MAX_ATTEMPTS ]; then
    echo -e "${RED}Replica set initialization did not complete within the allowed time.${NC}"
    docker logs mongo-init
    exit 1
  fi
done

# Verify replica set status
echo -e "${YELLOW}Verifying replica set status...${NC}"
REPLICA_SET_OK=false

for i in {1..10}; do
  # Check if a primary is elected and all nodes are in the replica set
  RS_STATUS=$(docker exec mongo1 mongosh --quiet --eval "rs.status().ok" 2>/dev/null || echo "0")
  PRIMARY=$(docker exec mongo1 mongosh --quiet --eval "rs.isMaster().primary" 2>/dev/null | tr -d '\r')
  
  if [ "$RS_STATUS" == "1" ] && [ -n "$PRIMARY" ] && [ "$PRIMARY" != "null" ]; then
    echo -e "${GREEN}Replica set is healthy with primary: $PRIMARY${NC}"
    REPLICA_SET_OK=true
    break
  fi
  
  echo -e "${YELLOW}Waiting for replica set to be fully initialized (Attempt $i/10)...${NC}"
  sleep 5
done

if [ "$REPLICA_SET_OK" != "true" ]; then
  echo -e "${RED}Failed to verify replica set status.${NC}"
  docker exec mongo1 mongosh --eval "rs.status()"
  exit 1
fi

# Start the application service
echo -e "${GREEN}Starting application service...${NC}"
docker-compose up -d sample-api

# Wait for application to be healthy
echo -e "${YELLOW}Waiting for application to be healthy...${NC}"
for i in {1..30}; do
  API_STATUS=$(docker inspect --format='{{.State.Health.Status}}' sample-api 2>/dev/null || echo "not_running")
  
  if [ "$API_STATUS" == "healthy" ]; then
    echo -e "${GREEN}Application is healthy!${NC}"
    break
  fi
  
  echo -e "${YELLOW}Waiting for application to be healthy (Attempt $i/$MAX_ATTEMPTS)...${NC}"
  echo -e "  sample-api: $API_STATUS"
  sleep $DELAY
  
  if [ $i -eq $MAX_ATTEMPTS ]; then
    echo -e "${RED}Application did not become healthy within the allowed time.${NC}"
    docker logs sample-api
    exit 1
  fi
done

echo -e "${GREEN}All services are up and running successfully!${NC}"
echo -e "${BLUE}===== Services Summary =====${NC}"
docker-compose ps

echo -e "${BLUE}===== API is available at: http://localhost:8080 =====${NC}" 