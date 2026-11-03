import { count, desc, eq, sql } from "drizzle-orm";
import { db, newId } from "./db.js";
import { ledgerEntries, type LedgerRow } from "./schema.js";

export type EntryInput = {
  UserId: string;
  ReferenceId: string;
  EntryType: "Credit" | "Debit";
  Amount: number;
  Currency: string;
  Description?: string | null;
};

export function toResponse(row: LedgerRow) {
  return {
    id: row.Id,
    userId: row.UserId,
    referenceId: row.ReferenceId,
    entryType: row.EntryType,
    amount: row.Amount,
    currency: row.Currency,
    description: row.Description,
    createdAtUtc: new Date(row.CreatedAtUtc).toISOString(),
  };
}

export async function createEntry(input: EntryInput) {
  const id = newId();
  await db.insert(ledgerEntries).values({
    Id: id,
    UserId: input.UserId,
    ReferenceId: input.ReferenceId.trim(),
    EntryType: input.EntryType,
    Amount: input.Amount,
    Currency: input.Currency.toUpperCase(),
    Description: input.Description?.trim() || null,
    CreatedAtUtc: new Date(),
  });
  const [row] = await db.select().from(ledgerEntries).where(eq(ledgerEntries.Id, id));
  if (!row) throw new Error("Ledger entry was not created.");
  return row;
}

export async function entriesForUser(userId: string, page: number, pageSize: number) {
  const [{ totalCount }] = await db
    .select({ totalCount: count() })
    .from(ledgerEntries)
    .where(eq(ledgerEntries.UserId, userId));
  const items = await db
    .select()
    .from(ledgerEntries)
    .where(eq(ledgerEntries.UserId, userId))
    .orderBy(desc(ledgerEntries.CreatedAtUtc))
    .limit(pageSize)
    .offset((page - 1) * pageSize);
  return { items, totalCount: Number(totalCount) };
}

export async function addTransferEntries(event: Record<string, any>) {
  const transferId = event.transferId ?? event.TransferId;
  const reference = String(transferId);
  const [existing] = await db
    .select({ Id: ledgerEntries.Id })
    .from(ledgerEntries)
    .where(sql`${ledgerEntries.ReferenceId} = ${reference} AND ${ledgerEntries.EntryType} = 'Debit'`)
    .limit(1);
  if (existing) return;

  const amount = event.amount ?? event.Amount;
  const currency = event.currency ?? event.Currency;
  await db.insert(ledgerEntries).values([
    {
      Id: newId(), UserId: event.senderUserId ?? event.SenderUserId,
      ReferenceId: reference, EntryType: "Debit", Amount: amount, Currency: currency,
      Description: `Transfer ${reference}`, CreatedAtUtc: new Date(),
    },
    {
      Id: newId(), UserId: event.receiverUserId ?? event.ReceiverUserId,
      ReferenceId: reference, EntryType: "Credit", Amount: amount, Currency: currency,
      Description: `Transfer ${reference}`, CreatedAtUtc: new Date(),
    },
  ]);
}

export async function addWalletOperationEntry(event: Record<string, any>) {
  const operationId = event.operationId ?? event.OperationId;
  const operationType = event.operationType ?? event.OperationType;
  const reference = String(operationId);
  const entryType = operationType === "Deposit" ? "Credit" : "Debit";
  const [existing] = await db
    .select({ Id: ledgerEntries.Id })
    .from(ledgerEntries)
    .where(sql`${ledgerEntries.ReferenceId} = ${reference} AND ${ledgerEntries.EntryType} = ${entryType}`)
    .limit(1);
  if (existing) return;

  await db.insert(ledgerEntries).values({
    Id: newId(), UserId: event.userId ?? event.UserId,
    ReferenceId: reference, EntryType: entryType,
    Amount: event.amount ?? event.Amount,
    Currency: event.currency ?? event.Currency,
    Description: `${operationType} ${reference}`,
    CreatedAtUtc: new Date(event.completedAtUtc ?? event.CompletedAtUtc ?? Date.now()),
  });
}
