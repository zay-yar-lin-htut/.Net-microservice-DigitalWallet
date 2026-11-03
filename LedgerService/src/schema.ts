import {
  char,
  datetime,
  decimal,
  mysqlTable,
  text,
  varchar,
} from "drizzle-orm/mysql-core";

export const ledgerEntries = mysqlTable("LedgerEntries", {
  Id: char("Id", { length: 36 }).primaryKey(),
  UserId: char("UserId", { length: 36 }).notNull(),
  ReferenceId: varchar("ReferenceId", { length: 200 }).notNull(),
  EntryType: varchar("EntryType", { length: 10 }).notNull(),
  Amount: decimal("Amount", { precision: 18, scale: 2 }).notNull().$type<number>(),
  Currency: varchar("Currency", { length: 3 }).notNull(),
  Description: text("Description"),
  CreatedAtUtc: datetime("CreatedAtUtc", { mode: "date" }).notNull(),
});

export type LedgerRow = typeof ledgerEntries.$inferSelect;
