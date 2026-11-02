import { useEffect, useRef } from 'react'
import { API_BASE_URL } from '../config/env'

export type RealtimeEvent = {
  type: string
  data: Record<string, unknown>
}

export function useRealtime(token: string | null, onEvent: (event: RealtimeEvent) => void) {
  const callback = useRef(onEvent)
  callback.current = onEvent

  useEffect(() => {
    if (!token) return
    let socket: WebSocket | null = null
    let retryTimer: number | undefined
    let stopped = false
    let retry = 0

    const connect = () => {
      if (stopped) return
      const url = new URL(API_BASE_URL)
      url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:'
      url.pathname = `${url.pathname.replace(/\/$/, '')}/ws`
      url.search = `?token=${encodeURIComponent(token)}`
      socket = new WebSocket(url.toString())
      socket.onopen = () => { retry = 0 }
      socket.onmessage = (message) => {
        try { callback.current(JSON.parse(message.data) as RealtimeEvent) } catch { /* ignore malformed events */ }
      }
      socket.onclose = () => {
        if (stopped) return
        retry += 1
        retryTimer = window.setTimeout(connect, Math.min(30000, 1000 * 2 ** Math.min(retry, 5)))
      }
    }

    connect()
    return () => {
      stopped = true
      if (retryTimer) window.clearTimeout(retryTimer)
      socket?.close()
    }
  }, [token])
}
