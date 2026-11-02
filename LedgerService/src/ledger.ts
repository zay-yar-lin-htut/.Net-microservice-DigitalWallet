import { pool, newId, type LedgerRow } from "./db.js";
export type EntryInput = {
  UserId: string;
  ReferenceId: string;
  EntryType: "Credit" | "Debit";
  Amount: number;
  Currency: string;
  Description?: string | null;
};
export async function createEntry(input: EntryInput) {
  const id = newId();
  await pool.execute(
    "INSERT INTO LedgerEntries (Id, UserId, ReferenceId, EntryType, Amount, Currency, Description, CreatedAtUtc) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
    [
      id,
      input.UserId,
      input.ReferenceId.trim(),
      input.EntryType,
      input.Amount,
      input.Currency.toUpperCase(),
      input.Description?.trim() || null,
      new Date(),
    ],
  );
  const [rows] = await pool.execute(
    "SELECT * FROM LedgerEntries WHERE Id = ?",
    [id],
  );
  const row = (rows as LedgerRow[])[0];
  if (!row) throw new Error("Ledger entry was not created.");
  return row;
}
export async function entriesForUser(
  userId: string,
  page: number,
  pageSize: number,
) {
  const [countRows] = await pool.execute(
    "SELECT COUNT(*) AS totalCount FROM LedgerEntries WHERE UserId = ?",
    [userId],
  );
  const totalCount = Number(
    (countRows as { totalCount: number }[])[0]?.totalCount ?? 0,
  );
  const offset = (page - 1) * pageSize;
  const [rows] = await pool.execute(
    "SELECT * FROM LedgerEntries WHERE UserId = ? ORDER BY CreatedAtUtc DESC LIMIT ? OFFSET ?",
    [userId, pageSize, offset],
  );
  return { items: rows as LedgerRow[], totalCount };
}
export async function addTransferEntries(event: Record<string, any>) {
  const transferId = event.transferId ?? event.TransferId;
  const reference = String(transferId);
  const [existing] = await pool.execute(
    "SELECT Id FROM LedgerEntries WHERE ReferenceId = ? AND EntryType = ? LIMIT 1",
    [reference, "Debit"],
  );
  if ((existing as unknown[]).length) return;
  const amount = event.amount ?? event.Amount;
  const currency = event.currency ?? event.Currency;
  await pool.query(
    "INSERT INTO LedgerEntries (Id, UserId, ReferenceId, EntryType, Amount, Currency, Description, CreatedAtUtc) VALUES (?, ?, ?, ?, ?, ?, ?, ?), (?, ?, ?, ?, ?, ?, ?, ?)",
    [
      newId(),
      event.senderUserId ?? event.SenderUserId,
      reference,
      "Debit",
      amount,
      currency,
      `Transfer ${reference}`,
      new Date(),
      newId(),
      event.receiverUserId ?? event.ReceiverUserId,
      reference,
      "Credit",
      amount,
      currency,
      `Transfer ${reference}`,
      new Date(),
    ],
  );
}
export async function addWalletOperationEntry(event: Record<string, any>) {
  const operationId = event.operationId ?? event.OperationId;
  const operationType = event.operationType ?? event.OperationType;
  const reference = String(operationId);
  const entryType = operationType === "Deposit" ? "Credit" : "Debit";
  const [existing] = await pool.execute(
    "SELECT Id FROM LedgerEntries WHERE ReferenceId = ? AND EntryType = ? LIMIT 1",
    [reference, entryType],
  );
  if ((existing as unknown[]).length) return;
  await pool.execute(
    "INSERT INTO LedgerEntries (Id, UserId, ReferenceId, EntryType, Amount, Currency, Description, CreatedAtUtc) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
    [
      newId(),
      event.userId ?? event.UserId,
      reference,
      entryType,
      event.amount ?? event.Amount,
      event.currency ?? event.Currency,
      `${operationType} ${reference}`,
      new Date(event.completedAtUtc ?? event.CompletedAtUtc ?? Date.now()),
    ],
  );
}
