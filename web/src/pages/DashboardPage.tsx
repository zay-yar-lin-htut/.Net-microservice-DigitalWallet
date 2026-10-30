import { Children, useCallback, useEffect, useState } from "react";
import { AppNav } from "../components/AppNav";
import { ServiceStatusCard } from "../components/ServiceStatusCard";
import { getServiceHealth, apiServices } from "../api/services/healthApi";
import {
  createWallet,
  deposit,
  getWallet,
  withdraw,
} from "../api/services/walletApi";
import { createTransfer, getTransfers } from "../api/services/transferApi";
import { getLedgerEntries } from "../api/services/ledgerApi";
import type { ServiceStatus } from "../types/api";
import type { LedgerEntry, Transfer, Wallet } from "../types/domain";

const money = (amount: number, currency = "MMK") =>
  `${amount.toLocaleString()} ${currency}`;
const date = (value: string) => new Date(value).toLocaleString();

export function DashboardPage() {
  const [status, setStatus] = useState<Record<string, ServiceStatus>>({});
  const [wallet, setWallet] = useState<Wallet | null>(null);
  const [transfers, setTransfers] = useState<Transfer[]>([]);
  const [ledger, setLedger] = useState<LedgerEntry[]>([]);
  const [error, setError] = useState("");
  const [amount, setAmount] = useState("");
  const [sendAmount, setSendAmount] = useState("");
  const [receiver, setReceiver] = useState("");
  const [busy, setBusy] = useState(false);

  const loadAccount = useCallback(async () => {
    const [walletResult, transferResult, ledgerResult] =
      await Promise.allSettled([
        getWallet(),
        getTransfers(),
        getLedgerEntries(),
      ]);
    if (walletResult.status === "fulfilled") setWallet(walletResult.value);
    if (transferResult.status === "fulfilled")
      setTransfers(transferResult.value);
    if (ledgerResult.status === "fulfilled") setLedger(ledgerResult.value);
  }, []);

  useEffect(() => {
    let active = true;
    void Promise.all(
      apiServices.map(async (service) => {
        try {
          await getServiceHealth(service.path);
          if (active)
            setStatus((current) => ({ ...current, [service.name]: "healthy" }));
        } catch {
          if (active)
            setStatus((current) => ({ ...current, [service.name]: "offline" }));
        }
      }),
    );
    void loadAccount();
    return () => {
      active = false;
    };
  }, [loadAccount]);

  async function handleWalletAction(action: "deposit" | "withdraw") {
    const value = Number(amount);
    if (!value || value <= 0)
      return setError("Enter an amount greater than zero.");
    setBusy(true);
    setError("");
    try {
      setWallet(
        await (action === "deposit" ? deposit(value) : withdraw(value)),
      );
      setAmount("");
    } catch {
      setError(`Unable to ${action} funds. Please try again.`);
    } finally {
      setBusy(false);
    }
  }

  async function handleTransfer(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const value = Number(sendAmount);
    if (!receiver || !value || value <= 0)
      return setError("Enter a receiver user ID and a valid amount.");
    setBusy(true);
    setError("");
    try {
      await createTransfer({
        receiverUserId: receiver,
        sendAmount: value,
        currency: wallet?.currency ?? "MMK",
      });
      setTransfers(await getTransfers());
      setSendAmount("");
      setReceiver("");
    } catch {
      setError("Transfer failed. Check the receiver ID and wallet balance.");
    } finally {
      setBusy(false);
    }
  }

  async function handleCreateWallet() {
    setBusy(true);
    setError("");
    try {
      setWallet(await createWallet());
    } catch {
      setError("Unable to create wallet.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <AppNav />
      <main className="mx-auto max-w-6xl px-6 py-8 lg:px-8">
        <header>
          <p className="text-sm font-semibold uppercase tracking-[0.25em] text-indigo-400">
            Dashboard
          </p>
          <h1 className="mt-2 text-3xl font-bold">Your Digital Wallet</h1>
        </header>
        {error && (
          <p className="mt-6 rounded-lg bg-rose-500/10 p-4 text-sm text-rose-300">
            {error}
          </p>
        )}
        <section className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {apiServices.map((service) => (
            <ServiceStatusCard
              key={service.name}
              name={service.name}
              status={status[service.name] ?? "checking"}
            />
          ))}
        </section>
        <section className="mt-8 rounded-2xl bg-indigo-600 p-6">
          <p className="text-sm text-indigo-100">Available balance</p>
          <p className="mt-2 text-4xl font-bold">
            {wallet ? money(wallet.balance, wallet.currency) : "No wallet"}
          </p>
          {!wallet && (
            <button
              onClick={handleCreateWallet}
              disabled={busy}
              className="mt-5 rounded-lg bg-white px-4 py-2 font-semibold text-indigo-700"
            >
              Create wallet
            </button>
          )}
        </section>
        <section className="mt-8 grid gap-6 lg:grid-cols-2">
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-xl font-semibold">Wallet actions</h2>
            <input
              type="number"
              min="0.01"
              step="0.01"
              placeholder="Amount"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
              className="mt-4 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-3 outline-none focus:border-indigo-400"
            />
            <div className="mt-3 flex gap-3">
              <button
                disabled={busy || !wallet}
                onClick={() => void handleWalletAction("deposit")}
                className="flex-1 rounded-lg bg-emerald-600 px-4 py-3 font-semibold disabled:opacity-50"
              >
                Deposit
              </button>
              <button
                disabled={busy || !wallet}
                onClick={() => void handleWalletAction("withdraw")}
                className="flex-1 rounded-lg bg-amber-600 px-4 py-3 font-semibold disabled:opacity-50"
              >
                Withdraw
              </button>
            </div>
          </div>
          <form
            onSubmit={handleTransfer}
            className="rounded-2xl border border-slate-800 bg-slate-900 p-6"
          >
            <h2 className="text-xl font-semibold">Send money</h2>
            <input
              required
              placeholder="Receiver user ID"
              value={receiver}
              onChange={(event) => setReceiver(event.target.value)}
              className="mt-4 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-3 outline-none focus:border-indigo-400"
            />
            <input
              required
              type="number"
              min="0.01"
              step="0.01"
              placeholder="Amount"
              value={sendAmount}
              onChange={(event) => setSendAmount(event.target.value)}
              className="mt-3 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-3 outline-none focus:border-indigo-400"
            />
            <button
              disabled={busy || !wallet}
              className="mt-3 w-full rounded-lg bg-indigo-500 px-4 py-3 font-semibold disabled:opacity-50"
            >
              Send transfer
            </button>
          </form>
        </section>
        <section className="mt-8 grid gap-6 lg:grid-cols-2">
          <DataList title="Recent transfers" empty="No transfers yet.">
            {transfers.map((item) => (
              <li
                key={item.id}
                className="flex justify-between border-b border-slate-800 py-3 text-sm"
              >
                <span>
                  <strong className="block">{item.status}</strong>
                  <span className="text-slate-400">
                    To: {item.receiverUserId}
                  </span>
                </span>
                <span>{money(item.amount, item.currency)}</span>
              </li>
            ))}
          </DataList>
          <DataList title="Ledger activity" empty="No ledger entries yet.">
            {ledger.map((item) => (
              <li
                key={item.id}
                className="flex justify-between border-b border-slate-800 py-3 text-sm"
              >
                <span>
                  <strong className="block">{item.entryType}</strong>
                  <span className="text-slate-400">
                    {item.description ?? date(item.createdAtUtc)}
                  </span>
                </span>
                <span>{money(item.amount, item.currency)}</span>
              </li>
            ))}
          </DataList>
        </section>
      </main>
    </div>
  );
}

function DataList({
  title,
  empty,
  children,
}: {
  title: string;
  empty: string;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
      <h2 className="text-xl font-semibold">{title}</h2>
      {Children.count(children) > 0 ? (
        <ul className="mt-3">{children}</ul>
      ) : (
        <p className="mt-4 text-sm text-slate-400">{empty}</p>
      )}
    </div>
  );
}
