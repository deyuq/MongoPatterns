# Outbox Pattern Implementation in MongoRepository

## Overview

The outbox pattern is a reliable messaging solution that ensures both data consistency and reliable message delivery in distributed systems. When a service needs to both update its database and publish messages/events, it uses a single transaction to both update the application data and store outgoing messages in an "outbox" table. A separate process then reads from the outbox to process these messages.

## Pattern Workflow

```
┌────────────────────────────────────────────────────────────────────┐
│                         MongoDB Database                           │
│                                                                    │
│   ┌─────────────────┐             ┌──────────────────────┐         │
│   │                 │             │                      │         │
│   │  Application    │◄────────────┤   Outbox Collection  │         │
│   │  Collections    │    Single   │                      │         │
│   │                 │  Transaction│  ┌────────────────┐  │         │
│   └─────────────────┘             │  │ Message Status │  │         │
│           ▲                       │  │ ┌────────────┐ │  │         │
│           │                       │  │ │  Pending   │ │  │         │
└───────────┼───────────────────────┘  │ └────────────┘ │  │         │
            │                           │ ┌────────────┐ │  │         │
┌───────────┼───────────────────────┐  │ │ Processing │ │  │         │
│           │                       │  │ └────────────┘ │  │         │
│ ┌─────────┴───────────┐          │  │ ┌────────────┐ │  │         │
│ │                     │          │  │ │  Processed │ │  │         │
│ │     Application     │          │  │ └────────────┘ │  │         │
│ │       Service       │──────────┼──┤ ┌────────────┐ │  │         │
│ │                     │  Write   │  │ │   Failed   │ │  │         │
│ └─────────────────────┘ Messages │  │ └────────────┘ │  │         │
│           │                      │  │ ┌────────────┐ │  │         │
│           │                      │  │ │ Abandoned  │ │  │         │
│           ▼                      │  │ └────────────┘ │  │         │
│ ┌─────────────────────┐          │  └────────────────┘  │         │
│ │                     │          │                      │         │
│ │    IOutboxService   │──────────┘                      │         │
│ │                     │                                 │         │
│ └─────────────────────┘                                 │         │
│                                                         │         │
│                       ┌──────────────────────┐          │         │
│                       │                      │          │         │
│                       │   OutboxProcessor    │◄─────────┘         │
│                       │  (BackgroundService) │                    │
│                       │                      │                    │
│                       └──────────────────────┘                    │
│                                  │                                │
│                                  ▼                                │
│                       ┌──────────────────────┐                    │
│                       │                      │                    │
│                       │   Message Handlers   │                    │
│                       │                      │                    │
│                       └──────────────────────┘                    │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

## Components

1. **IOutboxService**:
   - Provides methods to add messages to the outbox
   - Supports adding messages within transactions
   - Automatically serializes messages to JSON

2. **OutboxProcessor**:
   - Runs as a BackgroundService
   - Periodically checks for pending messages
   - Handles message processing and status updates
   - Implements exponential backoff for retries

3. **Message Handlers**:
   - Process specific message types
   - Registered with dependency injection
   - Implement business logic for each message type

4. **Outbox Message**:
   - Stored in MongoDB
   - Contains message content, type, status, and metadata
   - Tracks retry attempts and processing history

## Message Flow

1. **Message Creation and Storage**:
   - Application service creates a domain event/message
   - IOutboxService serializes and stores it in the outbox collection
   - If within a transaction, the message is added as part of that transaction

2. **Message Processing**:
   - OutboxProcessor polls for pending messages
   - Changes message status to "Processing"
   - Finds appropriate handler based on message type
   - Deserializes message and passes to handler

3. **Status Updates**:
   - Success → Status changed to "Processed"
   - Temporary failure → Status changed to "Failed", scheduled for retry
   - Permanent failure → Status changed to "Abandoned" after max retries

## Key Benefits

1. **Reliability**: Ensures messages are not lost during system failures
2. **Consistency**: Guarantees database changes and messages are saved atomically
3. **Decoupling**: Separates message creation from processing
4. **Resilience**: Handles temporary failures with retries
5. **Monitoring**: Provides visibility into message processing status

## Implementation Details

The MongoRepository implementation of the outbox pattern provides:

- Automatic creation of required MongoDB indexes
- Configurable processing intervals and batch sizes
- Exponential backoff for failed message processing
- Comprehensive logging and error handling
- Support for MongoDB transactions via the Unit of Work pattern
- Type-safe message handling with generic handlers 