import mysql from "mysql2/promise";
import { drizzle } from "drizzle-orm/mysql2";

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

export const db = drizzle({ client: pool });

export function newId() {
  return crypto.randomUUID();
}
