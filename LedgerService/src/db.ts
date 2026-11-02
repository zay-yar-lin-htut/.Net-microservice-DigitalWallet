import mysql from "mysql2/promise";
export const pool = mysql.createPool({
  host: process.env.DB_HOST ?? "mysql-ledger",
  port: Number(process.env.DB_PORT ?? 3306),
  database: process.env.DB_DATABASE ?? "ledger_db",
  user: process.env.DB_USERNAME ?? "ledger_app",
  password: process.env.DB_PASSWORD ?? "",
  waitForConnections: true,
  connectionLimit: 10,
  decimalNumbers: true,
});
export type LedgerRow = {
  Id: string;
  UserId: string;
  ReferenceId: string;
  EntryType: "Credit" | "Debit";
  Amount: number;
  Currency: string;
  Description: string | null;
  CreatedAtUtc: Date;
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
export function newId() {
  return crypto.randomUUID();
}
