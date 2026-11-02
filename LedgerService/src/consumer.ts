import amqp from "amqplib";
import { addTransferEntries, addWalletOperationEntry } from "./ledger.js";
const connection = await amqp.connect({
  hostname: process.env.RABBITMQ_HOST ?? "rabbitmq",
  port: Number(process.env.RABBITMQ_PORT ?? 5672),
  username: process.env.RABBITMQ_USER ?? "guest",
  password: process.env.RABBITMQ_PASSWORD ?? "guest",
});
const channel = await connection.createChannel();
const transferQueue = "ledger-transfer-completed",
  walletQueue = "ledger-wallet-operation-completed",
  transferExchange = "DigitalWallet.Contracts:TransferCompletedEvent",
  walletExchange = "DigitalWallet.Contracts:WalletOperationCompletedEvent";
await channel.assertExchange(transferExchange, "fanout", { durable: true });
await channel.assertExchange(walletExchange, "fanout", { durable: true });
await channel.assertQueue(transferQueue, { durable: true });
await channel.assertQueue(walletQueue, { durable: true });
await channel.bindQueue(transferQueue, transferExchange, "");
await channel.bindQueue(walletQueue, walletExchange, "");
await channel.prefetch(1);
console.log("Waiting for transfer and wallet events...");
async function handle(
  message: amqp.ConsumeMessage | null,
  type: "transfer" | "wallet",
) {
  if (!message) return;
  try {
    const envelope = JSON.parse(message.content.toString());
    const event = envelope.message ?? envelope.Message ?? envelope;
    if (type === "transfer") await addTransferEntries(event);
    else await addWalletOperationEntry(event);
    channel.ack(message);
  } catch (error) {
    console.error(error);
    channel.nack(message, false, true);
  }
}
await channel.consume(
  transferQueue,
  (message) => void handle(message, "transfer"),
);
await channel.consume(walletQueue, (message) => void handle(message, "wallet"));
