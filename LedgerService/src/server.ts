import Fastify from "fastify";
import cors from "@fastify/cors";
import { authenticate, assertInternal } from "./auth.js";
import { createEntry, entriesForUser } from "./ledger.js";
import { toResponse } from "./db.js";
const app = Fastify({ logger: true });
await app.register(cors, { origin: true });
app.get("/api/ledger/status", async () => ({
  service: "Ledger Service",
  status: "Healthy",
}));
app.get<{ Querystring: { page?: string; pageSize?: string } }>(
  "/api/ledger/me",
  async (request, reply) => {
    try {
      const page = Math.max(1, Number(request.query.page ?? 1) || 1),
        pageSize = Math.min(
          100,
          Math.max(1, Number(request.query.pageSize ?? 5) || 5),
        ),
        userId = await authenticate(request),
        result = await entriesForUser(userId, page, pageSize);
      return {
        items: result.items.map(toResponse),
        page,
        pageSize,
        totalCount: result.totalCount,
        totalPages: Math.ceil(result.totalCount / pageSize),
      };
    } catch (error) {
      return reply
        .code(401)
        .send({
          message: error instanceof Error ? error.message : "Unauthenticated.",
        });
    }
  },
);
app.post<{ Body: Record<string, unknown> }>(
  "/api/ledger/entries",
  async (request, reply) => {
    try {
      const entry = await createEntry({
        UserId: await authenticate(request),
        ...input(request.body),
      });
      return reply
        .code(201)
        .header("Location", `/api/ledger/entries/${entry.Id}`)
        .send(toResponse(entry));
    } catch (error) {
      return reply
        .code(
          error instanceof Error && error.message === "Unauthenticated."
            ? 401
            : 400,
        )
        .send({
          message: error instanceof Error ? error.message : "Invalid request.",
        });
    }
  },
);
app.post<{ Body: Record<string, unknown> }>(
  "/api/ledger/internal/entries",
  async (request, reply) => {
    try {
      assertInternal(request);
      const body = request.body;
      const userId = body.userId ?? body.UserId;
      if (typeof userId !== "string") throw new Error("UserId is required.");
      const entry = await createEntry({ UserId: userId, ...input(body) });
      return reply
        .code(201)
        .header("Location", `/api/ledger/entries/${entry.Id}`)
        .send(toResponse(entry));
    } catch (error) {
      return reply
        .code(
          error instanceof Error && error.message === "Internal access denied."
            ? 401
            : 400,
        )
        .send({
          message: error instanceof Error ? error.message : "Invalid request.",
        });
    }
  },
);
function input(body: Record<string, unknown>) {
  const referenceId = body.referenceId ?? body.ReferenceId,
    entryType = body.entryType ?? body.EntryType,
    amount = body.amount ?? body.Amount,
    currency = body.currency ?? body.Currency ?? "MMK";
  if (typeof referenceId !== "string" || !referenceId.trim())
    throw new Error("ReferenceId is required.");
  if (entryType !== "Credit" && entryType !== "Debit")
    throw new Error("EntryType must be Credit or Debit.");
  if (typeof amount !== "number" || amount <= 0)
    throw new Error("Amount must be greater than zero.");
  if (typeof currency !== "string" || currency.length !== 3)
    throw new Error("Currency must have 3 characters.");
  return {
    ReferenceId: referenceId,
    EntryType: entryType,
    Amount: amount,
    Currency: currency,
    Description: String(body.description ?? body.Description ?? "") || null,
  } as const;
}
await app.listen({ host: "0.0.0.0", port: 8060 });
