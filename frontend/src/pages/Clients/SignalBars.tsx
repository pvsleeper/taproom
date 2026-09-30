import { signalLevel } from '../../lib/format'

const LEVEL_COLOR: Record<string, string> = {
  good: 'bg-online',
  fair: 'bg-warn',
  poor: 'bg-danger',
}

const LEVEL_BARS: Record<string, number> = {
  good: 4,
  fair: 3,
  poor: 2,
}

interface SignalBarsProps {
  rssi: number | null
}

export function SignalBars({ rssi }: SignalBarsProps) {
  if (rssi === null) {
    return <span className="text-text-muted">—</span>
  }

  const level = signalLevel(rssi)!
  const activeBars = LEVEL_BARS[level]
  const color = LEVEL_COLOR[level]

  return (
    <div className="flex items-center gap-1.5">
      <div className="flex items-end gap-0.5" aria-hidden>
        {[0, 1, 2, 3].map((i) => (
          <span
            key={i}
            className={`w-1 rounded-sm ${i < activeBars ? color : 'bg-border'}`}
            style={{ height: `${4 + i * 3}px` }}
          />
        ))}
      </div>
      <span className="font-mono text-xs text-text-muted">{rssi} dBm</span>
    </div>
  )
}
