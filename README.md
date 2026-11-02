# Digital Wallet

A web application for managing money between digital wallets.

Users can:

- register and log in;
- create a wallet;
- deposit and withdraw money;
- send money to another user;
- view transfers and ledger history;
- receive live operation updates.

## Run the project

### 1. Install requirements

- Docker Desktop with Docker Compose
- Node.js and pnpm

### 2. Create `.env`

Create this file in the project root. Use real, private values in development. Do not commit it.

```env
MYSQL_ROOT_PASSWORD=change-me
IDENTITY_DB_PASSWORD=change-me
WALLET_DB_PASSWORD=change-me
TRANSFER_DB_PASSWORD=change-me
LEDGER_DB_PASSWORD=change-me
RABBITMQ_USER=wallet
RABBITMQ_PASSWORD=change-me
INTERNAL_SERVICE_KEY=change-me
JWT_SECRET=use-a-random-secret-at-least-32-characters
```

`JWT_SECRET` must be at least 32 characters.

### 3. Create the databases

Run the existing migrations once:

```bash
docker compose --profile tools run --rm identity-migrator database update
docker compose --profile tools run --rm wallet-migrator database update
docker compose --profile tools run --rm transfer-migrator database update
```

### 4. Start the backend

```bash
docker compose up --build
```

The API is now available at **http://localhost:8080**.

### 5. Start the web app

In a second terminal:

```bash
cd web
pnpm install
pnpm dev
```

Open **http://localhost:5173**. The frontend uses **http://localhost:8080** by default; override it with `VITE_API_BASE_URL`.

## Project parts

| Folder/service | Purpose |
|---|---|
| `web` | React + TypeScript user interface |
| `GateWayAPI` | Routes frontend requests to the services |
| `IdentityService` | Registration, login, and JWT tokens |
| `WalletService` | Wallets, balances, deposits, and withdrawals |
| `TransferService` | Transfers between users |
| `LedgerService` | Transaction history |
| `RealtimeService` | Live WebSocket notifications |
| MySQL | Stores identity, wallet, transfer, and ledger data |
| RabbitMQ | Sends events between services |

## API basics

All API requests use `http://localhost:8080`.

| Area | Endpoints |
|---|---|
| Account | `POST /api/identity/register` · `POST /api/identity/login` |
| Wallet | `POST /api/wallet` · `GET /api/wallet/me` |
| Money | `POST /api/wallet/deposit` · `POST /api/wallet/withdraw` |
| Transfers | `POST /api/transfer` · `GET /api/transfer/me` · `GET /api/transfer/{id}` |
| History | `GET /api/ledger/me` |
| Health | `GET /health` and service `/status` endpoints |
| Live updates | `ws://localhost:8080/ws?token=<JWT>` |

After login, send the token with protected requests:

```http
Authorization: Bearer <JWT>
```

Transfers also require an `Idempotency-Key` header. Deposits, withdrawals, and transfers run asynchronously, so check the returned operation or wait for a WebSocket update.

## Useful commands

```bash
# Frontend
cd web && pnpm build

# Ledger / realtime services
cd LedgerService && pnpm install && pnpm build
cd RealtimeService && pnpm install && pnpm build

# .NET services
dotnet build DigitalWallet.slnx
```

Useful local tools:

- Seq logs: **http://localhost:5343**
- RabbitMQ dashboard: **http://localhost:15672**
