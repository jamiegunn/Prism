import { useEffect, useRef, useState, useCallback } from 'react'

interface UseSSEOptions {
  onMessage?: (data: string) => void
  onError?: (error: Event) => void
  reconnectInterval?: number
  maxRetries?: number
}

interface UseSSEReturn {
  data: string | null
  error: Event | null
  isConnected: boolean
  close: () => void
}

export function useSSE(url: string | null, options: UseSSEOptions = {}): UseSSEReturn {
  const { onMessage, onError, reconnectInterval = 3000, maxRetries = 5 } = options
  const [data, setData] = useState<string | null>(null)
  const [error, setError] = useState<Event | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  const eventSourceRef = useRef<EventSource | null>(null)
  const retriesRef = useRef(0)
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const close = useCallback(() => {
    if (reconnectTimerRef.current) {
      clearTimeout(reconnectTimerRef.current)
      reconnectTimerRef.current = null
    }
    if (eventSourceRef.current) {
      eventSourceRef.current.close()
      eventSourceRef.current = null
    }
    setIsConnected(false)
  }, [])

  useEffect(() => {
    if (!url) {
      // Inline cleanup instead of calling close() to avoid setState in effect body
      if (reconnectTimerRef.current) {
        clearTimeout(reconnectTimerRef.current)
        reconnectTimerRef.current = null
      }
      if (eventSourceRef.current) {
        eventSourceRef.current.close()
        eventSourceRef.current = null
      }
      return
    }

    function connect() {
      const eventSource = new EventSource(url!)
      eventSourceRef.current = eventSource

      eventSource.onopen = () => {
        setIsConnected(true)
        setError(null)
        retriesRef.current = 0
      }

      eventSource.onmessage = (event: MessageEvent) => {
        const messageData = event.data as string
        setData(messageData)
        onMessage?.(messageData)
      }

      eventSource.onerror = (err: Event) => {
        setError(err)
        setIsConnected(false)
        onError?.(err)
        eventSource.close()

        if (retriesRef.current < maxRetries) {
          retriesRef.current += 1
          reconnectTimerRef.current = setTimeout(connect, reconnectInterval)
        }
      }
    }

    connect()

    return () => {
      close()
    }
  }, [url, onMessage, onError, reconnectInterval, maxRetries, close])

  return { data, error, isConnected, close }
}
