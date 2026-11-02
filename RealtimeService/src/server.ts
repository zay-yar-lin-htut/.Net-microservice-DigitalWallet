import Fastify from 'fastify';
import websocket from '@fastify/websocket';
import amqp from 'amqplib';
import { jwtVerify } from 'jose';
import type { WebSocket } from 'ws';

type Client = { userId: string; socket: WebSocket };
type RealtimeEvent = { type: string; data: Record<string, unknown> };

const app = Fastify({ logger: true });
await app.register(websocket);
const clients = new Map<string, Set<WebSocket>>();
const jwtSecret = new TextEncoder().encode(process.env.JWT_SECRET ?? '');

app.get('/health', async () => ({ service: 'Realtime Service', status: 'Healthy' }));

app.get('/ws', { websocket: true }, async (socket, request) => {
  const token = typeof request.query === 'object' && request.query !== null
    ? (request.query as { token?: string }).token
    : undefined;
  const userId = await getUserId(token);
  if (!userId) {
    socket.close(1008, 'Unauthenticated');
    return;
  }

  const userSockets = clients.get(userId) ?? new Set<WebSocket>();
  userSockets.add(socket);
  clients.set(userId, userSockets);
  socket.send(JSON.stringify({ type: 'connected', data: { userId } }));

  socket.on('close', () => {
    userSockets.delete(socket);
    if (userSockets.size === 0) clients.delete(userId);
  });
});

async function getUserId(token?: string): Promise<string | null> {
  if (!token) return null;
  try {
    const { payload } = await jwtVerify(token, jwtSecret, {
      algorithms: ['HS256'],
      issuer: process.env.JWT_ISSUER ?? 'DigitalWallet.Identity',
      audience: process.env.JWT_AUDIENCE ?? 'DigitalWallet.Services',
    });
    return typeof payload.sub === 'string' ? payload.sub : null;
  } catch {
    return null;
  }
}

function publish(userId: string, event: RealtimeEvent) {
  for (const socket of clients.get(userId) ?? []) {
    if (socket.readyState === 1) socket.send(JSON.stringify(event));
  }
}

async function consumeEvents() {
  const connection = await amqp.connect({
    hostname: process.env.RABBITMQ_HOST ?? 'rabbitmq',
    port: Number(process.env.RABBITMQ_PORT ?? 5672),
    username: process.env.RABBITMQ_USER ?? 'guest',
    password: process.env.RABBITMQ_PASSWORD ?? 'guest',
  });
  const channel = await connection.createChannel();
  await channel.assertExchange('DigitalWallet.Contracts:WalletOperationCompletedEvent', 'fanout', { durable: true });
  await channel.assertExchange('DigitalWallet.Contracts:TransferCompletedEvent', 'fanout', { durable: true });
  await channel.assertQueue('realtime-wallet-operation-completed', { durable: true });
  await channel.assertQueue('realtime-transfer-completed', { durable: true });
  await channel.bindQueue('realtime-wallet-operation-completed', 'DigitalWallet.Contracts:WalletOperationCompletedEvent', '');
  await channel.bindQueue('realtime-transfer-completed', 'DigitalWallet.Contracts:TransferCompletedEvent', '');
  await channel.prefetch(10);

  await channel.consume('realtime-wallet-operation-completed', message => {
    if (!message) return;
    try {
      const envelope = JSON.parse(message.content.toString());
      const data = envelope.message ?? envelope.Message ?? envelope;
      const userId = data.userId ?? data.UserId;
      publish(userId, { type: 'wallet.operation.completed', data });
      publish(userId, { type: 'ledger.entry.created', data });
      channel.ack(message);
    } catch (error) {
      app.log.error(error);
      channel.nack(message, false, true);
    }
  });

  await channel.consume('realtime-transfer-completed', message => {
    if (!message) return;
    try {
      const envelope = JSON.parse(message.content.toString());
      const data = envelope.message ?? envelope.Message ?? envelope;
      const senderId = data.senderUserId ?? data.SenderUserId;
      const receiverId = data.receiverUserId ?? data.ReceiverUserId;
      publish(senderId, { type: 'transfer.completed', data });
      publish(receiverId, { type: 'transfer.completed', data });
      publish(senderId, { type: 'ledger.entry.created', data });
      publish(receiverId, { type: 'ledger.entry.created', data });
      channel.ack(message);
    } catch (error) {
      app.log.error(error);
      channel.nack(message, false, true);
    }
  });
}

await consumeEvents();
await app.listen({ host: '0.0.0.0', port: 8090 });
