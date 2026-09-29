import { AlertTriangle } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Area, ComposedChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { BandwidthPoint } from './useBandwidthBuffer'

type Unit = 'mbps' | 'MBps'
const UNIT_STORAGE_KEY = 'taproom.bandwidthUnit'

function readStoredUnit(): Unit {
  try {
    const stored = localStorage.getItem(UNIT_STORAGE_KEY)
    return stored === 'MBps' ? 'MBps' : 'mbps'
  } catch {
    return 'mbps'
  }
}

function formatRate(mbps: number, unit: Unit): string {
  const value = unit === 'MBps' ? mbps / 8 : mbps
  const suffix = unit === 'MBps' ? 'MB/s' : 'Mbps'
  return `${value < 10 ? value.toFixed(2) : value.toFixed(1)} ${suffix}`
}

interface ChartRow {
  time: number
  down: number | null
  up: number | null
}

interface BandwidthCardProps {
  points: BandwidthPoint[]
  currentDownBps: number | undefined
  currentUpBps: number | undefined
  sourceOk: boolean
}

export function BandwidthCard({ points, currentDownBps, currentUpBps, sourceOk }: BandwidthCardProps) {
  const [unit, setUnit] = useState<Unit>(readStoredUnit)

  function changeUnit(next: Unit) {
    setUnit(next)
    try {
      localStorage.setItem(UNIT_STORAGE_KEY, next)
    } catch {
      // ignore
    }
  }

  const chartData = useMemo(() => {
    const rows: ChartRow[] = []
    for (const p of points) {
      if (p.gapBefore) {
        rows.push({ time: p.time - 1, down: null, up: null })
      }
      rows.push({ time: p.time, down: p.downMbps, up: -p.upMbps })
    }
    return rows
  }, [points])

  const peakDown = Math.max(0, ...points.map((p) => p.downMbps))
  const peakUp = Math.max(0, ...points.map((p) => p.upMbps))

  return (
    <div className="rounded-lg border border-border p-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">Bandwidth</h2>
        <div className="flex rounded-md border border-border bg-surface p-0.5">
          {(['mbps', 'MBps'] as const).map((u) => (
            <button
              key={u}
              onClick={() => changeUnit(u)}
              className={`rounded px-2 py-1 text-xs font-medium transition-colors ${
                unit === u ? 'bg-accent-muted text-accent' : 'text-text-muted hover:text-text'
              }`}
            >
              {u === 'mbps' ? 'Mbps' : 'MB/s'}
            </button>
          ))}
        </div>
      </div>

      {!sourceOk && (
        <div className="mt-2 flex items-center gap-1.5 rounded-md border border-warn/30 bg-warn/10 px-2 py-1.5 text-xs text-warn">
          <AlertTriangle size={12} />
          Sampling paused — showing the last known rates
        </div>
      )}

      <div className="mt-3 flex flex-col gap-4 sm:flex-row">
        <div className="flex shrink-0 flex-col justify-center gap-3 sm:w-40">
          <div>
            <div className="text-xl font-semibold text-accent tabular-nums">
              ↓ {currentDownBps !== undefined ? formatRate(currentDownBps / 1_000_000, unit) : '–'}
            </div>
            <div className="text-[11px] text-text-muted">peak {formatRate(peakDown, unit)}</div>
          </div>
          <div>
            <div className="text-xl font-semibold text-warn tabular-nums">
              ↑ {currentUpBps !== undefined ? formatRate(currentUpBps / 1_000_000, unit) : '–'}
            </div>
            <div className="text-[11px] text-text-muted">peak {formatRate(peakUp, unit)}</div>
          </div>
        </div>

        <div className="h-32 flex-1">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={chartData} margin={{ top: 4, right: 4, bottom: 0, left: 4 }}>
              <XAxis dataKey="time" type="number" domain={['dataMin', 'dataMax']} hide />
              <YAxis
                hide
                domain={[
                  (min: number) => Math.min(min, -1),
                  (max: number) => Math.max(max, 1),
                ]}
              />
              <ReferenceLine y={0} stroke="var(--color-border)" />
              <Tooltip
                labelFormatter={() => ''}
                formatter={(value, name) => [
                  formatRate(Math.abs(Number(value) || 0), unit),
                  name === 'down' ? 'Download' : 'Upload',
                ]}
                contentStyle={{
                  background: 'var(--color-surface)',
                  border: '1px solid var(--color-border)',
                  borderRadius: 6,
                  fontSize: 12,
                }}
              />
              <Area
                type="monotone"
                dataKey="down"
                stroke="var(--color-accent)"
                fill="var(--color-accent)"
                fillOpacity={0.25}
                isAnimationActive={false}
                connectNulls={false}
              />
              <Area
                type="monotone"
                dataKey="up"
                stroke="var(--color-warn)"
                fill="var(--color-warn)"
                fillOpacity={0.25}
                isAnimationActive={false}
                connectNulls={false}
              />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  )
}
