# Caygnus Product Engineering Assignment

## Overview

This project implements **Problem 1: Resumable Realtime Conversation**.

The system provides a resumable conversational response flow using:

- ASP.NET Core Web API
- Entity Framework Core
- SQL Server persistence
- Server-Sent Events (SSE)
- Durable ordered run events
- Sequence-based replay and deduplication
- Automatic client reconnection
- Run recovery after application restart
- Angular frontend
- Deterministic fake response generation
- Automated .NET tests

The database is the durable source of truth for run history. SSE is used for realtime delivery to connected clients, while persisted run events allow disconnected clients to replay missed events.

---

## Project Structure

```text
CaygnusProductEngineering/
├── backend/
│   └── Caygnus.ResumableConversation.Api/
│       └── Caygnus.ResumableConversation.Api/
├── client/
│   └── caygnus-client/
├── tests/
│   └── Caygnus.Tests/
├── README.md
├── SUBMISSION.md
└── .gitignore
