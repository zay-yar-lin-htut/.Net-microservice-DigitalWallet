import Fastify from "fastify";
import amqp from "amqplib";
import { jwtVerify } from "jose";
import { Server } from "socket.io";

type RealtimeEvent = { type: string; data: Record<string, unknown> };

const app = Fastify({ logger: true });
const jwtSecret = new TextEncoder().encode(process.env.JWT_SECRET ?? "");
const io = new Server(app.server, {
  path: "/ws/socket.io",
  cors: { origin: true },
});

app.get("/health", async () => ({
  service: "Realtime Service",
  status: "Healthy",
}));

io.use(async (socket, next) => {
  const userId = await getUserId(socket.handshake.auth.token);
  if (!userId) return next(new Error("Unauthenticated"));
  socket.data.userId = userId;
  next();
});

io.on("connection", (socket) => {
  const userId = socket.data.userId as string;
  socket.join(userId);
  socket.emit("connected", { userId });
});

async function getUserId(token?: string): Promise<string | null> {
  if (typeof token !== "string" || !token) return null;
  try {
    const { payload } = await jwtVerify(token, jwtSecret, {
      algorithms: ["HS256"],
      issuer: process.env.JWT_ISSUER ?? "DigitalWallet.Identity",
      audience: process.env.JWT_AUDIENCE ?? "DigitalWallet.Services",
    });
    return typeof payload.sub === "string" ? payload.sub : null;
  } catch {
    return null;
  }
}

function publish(userId: string, event: RealtimeEvent) {
  io.to(userId).emit(event.type, event.data);
}

async function consumeEvents() {
  const connection = await amqp.connect({
    hostname: process.env.RABBITMQ_HOST ?? "rabbitmq",
    port: Number(process.env.RABBITMQ_PORT ?? 5672),
    username: process.env.RABBITMQ_USER ?? "guest",
    password: process.env.RABBITMQ_PASSWORD ?? "guest",
  });
  const channel = await connection.createChannel();
  await channel.assertExchange("DigitalWallet.Contracts:WalletOperationCompletedEvent", "fanout", { durable: true });
  await channel.assertExchange("DigitalWallet.Contracts:TransferCompletedEvent", "fanout", { durable: true });
  await channel.assertQueue("realtime-wallet-operation-completed", { durable: true });
  await channel.assertQueue("realtime-transfer-completed", { durable: true });
  await channel.bindQueue("realtime-wallet-operation-completed", "DigitalWallet.Contracts:WalletOperationCompletedEvent", "");
  await channel.bindQueue("realtime-transfer-completed", "DigitalWallet.Contracts:TransferCompletedEvent", "");
  await channel.prefetch(10);

  await channel.consume("realtime-wallet-operation-completed", (message) => {
    if (!message) return;
    try {
      const envelope = JSON.parse(message.content.toString());
      const data = envelope.message ?? envelope.Message ?? envelope;
      const userId = data.userId ?? data.UserId;
      publish(userId, { type: "wallet.operation.completed", data });
      publish(userId, { type: "ledger.entry.created", data });
      channel.ack(message);
    } catch (error) {
      app.log.error(error);
      channel.nack(message, false, true);
    }
  });

  await channel.consume("realtime-transfer-completed", (message) => {
    if (!message) return;
    try {
      const envelope = JSON.parse(message.content.toString());
      const data = envelope.message ?? envelope.Message ?? envelope;
      const senderId = data.senderUserId ?? data.SenderUserId;
      const receiverId = data.receiverUserId ?? data.ReceiverUserId;
      publish(senderId, { type: "transfer.completed", data });
      publish(receiverId, { type: "transfer.completed", data });
      publish(senderId, { type: "ledger.entry.created", data });
      publish(receiverId, { type: "ledger.entry.created", data });
      channel.ack(message);
    } catch (error) {
      app.log.error(error);
      channel.nack(message, false, true);
    }
  });
}

await consumeEvents();
await app.listen({ host: "0.0.0.0", port: 8090 });
