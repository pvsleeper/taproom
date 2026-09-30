import { useEffect, useRef, useState } from 'react'
import { useDashboardWan } from '../../api/useDashboard'
import type { BandwidthPoint } from '../ClientDetail/useBandwidthBuffer'

const MAX_POINTS = 150
const GAP_THRESHOLD_MS = 10_000

export function useWanBuffer() {
  const query = useDashboardWan()
  const [points, setPoints] = useState<BandwidthPoint[]>([])
  const lastTimeRef = useRef<number | null>(null)

  useEffect(() => {
    const data = query.data
    if (!data) return

    const time = new Date(data.sampledAt).getTime()
    if (lastTimeRef.current === time) return

    const gapBefore = lastTimeRef.current !== null && time - lastTimeRef.current > GAP_THRESHOLD_MS
    lastTimeRef.current = time

    setPoints((prev) => {
      const next = [...prev, { time, downMbps: data.downBps / 1_000_000, upMbps: data.upBps / 1_000_000, gapBefore }]
      return next.length > MAX_POINTS ? next.slice(next.length - MAX_POINTS) : next
    })
  }, [query.data])

  return { points, current: query.data, isPending: query.isPending }
}
