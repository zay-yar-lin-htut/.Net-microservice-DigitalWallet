import { useEffect, useRef } from "react";
import { io } from "socket.io-client";
import { API_BASE_URL } from "../config/env";

export type RealtimeEvent = {
  type: string;
  data: Record<string, unknown>;
};

export function useRealtime(token: string | null, onEvent: (event: RealtimeEvent) => void) {
  const callback = useRef(onEvent);
  callback.current = onEvent;

  useEffect(() => {
    if (!token) return;

    const socket = io(API_BASE_URL, {
      path: "/ws/socket.io",
      auth: { token },
      transports: ["websocket"],
    });
    const eventTypes = [
      "wallet.operation.completed",
      "transfer.completed",
      "ledger.entry.created",
    ];

    eventTypes.forEach((type) => {
      socket.on(type, (data: Record<string, unknown>) => {
        callback.current({ type, data });
      });
    });

    return () => {
      socket.disconnect();
    };
  }, [token]);
}
