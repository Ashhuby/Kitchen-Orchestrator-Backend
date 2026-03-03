# PROJECT O: Distributed Real-Time Co-op Simulation

A server-authoritative, high-concurrency multiplayer "Overcooked-style" game platform. This project serves as a comprehensive showcase of modern backend engineering, featuring a microservices-inspired architecture, secure identity management, and real-time state synchronisation.

## System Architecture

The platform is divided into three distinct decoupled layers to ensure scalability and security:

1.  **Authoritative Game Server (Godot C# / Headless):** 
    *   Runs the physics and business logic at 60Hz.
    *   Handles UDP-based state replication and input validation.
    *   **Tech:** Godot 4.x, ENet, C#/.NET 8.
       
2.  **Persistence & Identity API (ASP.NET Core):** 
    *   A RESTful microservice handling "Off-tick" operations.
    *   Integrates with **Steamworks Web API** for OAuth2 session ticket verification.
    *   **Tech:** ASP.NET Core, JWT, Entity Framework Core.
    
3.  **Data Layer (PostgreSQL):** 
    *   Manages persistent user profiles, match history, and global analytics.
    *   **Tech:** PostgreSQL (Supabase), Redis (Caching).

##  Backend Key Features

*   **Server-Authoritative Logic:** All gameplay interactions (item pickups, timers, recipe completion) are validated server-side to prevent client-side tampering.
*   **Secure Steam Authentication:** Implements a secure handshake between the Game Client, Steam API, and custom Backend API to verify player identity without passwords.
*   **Transactional Data Integrity:** Uses ACID-compliant SQL transactions for updating player inventories and match results.
*   **Containerised Deployment:** The entire stack is orchestrated via **Docker**, allowing for consistent deployment across Linux VPS environments (Oracle Cloud).
*   **Observability:** Integrated structured logging via **Serilog** to track server health and game-logic anomalies in real-time.

##  Tech Stack

*   **Languages:** C# (.NET 8)
*   **Game Engine:** Godot 4.x (Headless Linux Export)
*   **Database:** PostgreSQL, Redis
*   **API:** ASP.NET Core REST API
*   **Infrastructure:** Docker, GitHub Actions (CI/CD), Oracle Cloud (OCI)
*   **Third-Party:** Steamworks SDK

---
*Developed as a collaborative project. Backend & Systems Architecture & Game logic by **Ashhuby**. Art & UI by **GummyBearKing123**.*
