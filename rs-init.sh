#!/bin/bash

MAX_ATTEMPTS=30
DELAY=5

echo "Waiting for MongoDB servers to be ready..."
for i in {1..30}; do
  if mongosh --host mongo1 --quiet --eval "db.adminCommand('ping').ok" > /dev/null; then
    echo "MongoDB server is up and running"
    break
  fi
  echo "Waiting for MongoDB server to start... ($i/$MAX_ATTEMPTS)"
  sleep $DELAY
done

echo "Initializing replica set..."
for i in {1..30}; do
  # Try to initiate the replica set
  result=$(mongosh --host mongo1 --quiet <<EOF
  var config = {
      "_id": "rs0",
      "version": 1,
      "members": [
          {
              "_id": 0,
              "host": "mongo1:27017",
              "priority": 3
          },
          {
              "_id": 1,
              "host": "mongo2:27017",
              "priority": 2
          },
          {
              "_id": 2,
              "host": "mongo3:27017",
              "priority": 1
          }
      ]
  };

  // Check if already initialized
  var status = rs.status();
  if (status.ok) {
    print("Replica set is already initialized");
    printjson(status);
    quit(0);
  }

  // Initialize the replica set
  var initResult = rs.initiate(config, { force: true });
  printjson(initResult);
  
  if (initResult.ok) {
    print("Replica set initialized successfully");
    quit(0);
  } else {
    print("Failed to initialize replica set: " + initResult.errmsg);
    quit(1);
  }
EOF
)

  echo "$result"
  
  # Check if replica set initialization was successful
  if echo "$result" | grep -q "Replica set initialized successfully" || echo "$result" | grep -q "Replica set is already initialized"; then
    echo "Replica set initialization completed successfully"
    break
  fi
  
  echo "Waiting for replica set initialization... ($i/$MAX_ATTEMPTS)"
  sleep $DELAY
done

# Wait for a primary to be elected
echo "Waiting for a primary to be elected..."
for i in {1..30}; do
  PRIMARY=$(mongosh --host mongo1 --quiet --eval "rs.isMaster().primary" | tr -d '\r')
  if [ -n "$PRIMARY" ] && [ "$PRIMARY" != "null" ]; then
    echo "Primary node elected: $PRIMARY"
    break
  fi
  echo "Waiting for primary election... ($i/$MAX_ATTEMPTS)"
  sleep $DELAY
done

# Final status check
echo "Final replica set status:"
mongosh --host mongo1 --quiet --eval "rs.status()" 