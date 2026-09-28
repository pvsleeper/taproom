import { Pause, Play } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { DnsResult } from '../../api/types'

const WINDOWS = [
  { label: '15 min', minutes: 15 },
  { label: '1 h', minutes: 60 },
  { label: '24 h', minutes: 1440 },
]

interface DnsPanelProps {
  minutes: number
  onMinutesChange: (minutes: number) => void
  data: DnsResult | undefined
  isPending: boolean
  onDomainClick: (domain: string) => void
}

export function DnsPanel({ minutes, onMinutesChange, data, isPending, onDomainClick }: DnsPanelProps) {
  const [paused, setPaused] = useState(false)
  const [frozenRecent, setFrozenRecent] = useState<DnsResult['recent']>([])

  const recent = useMemo(() => {
    if (paused) return frozenRecent
    return data?.recent ?? []
  }, [paused, frozenRecent, data])

  function togglePause() {
    if (!paused) setFrozenRecent(data?.recent ?? [])
    setPaused((p) => !p)
  }

  const maxCount = Math.max(1, ...(data?.topDomains.map((d) => d.count) ?? [1]))
  const blockedPct = data && data.totals.queries > 0 ? Math.round((data.totals.blocked / data.totals.queries) * 100) : 0

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border p-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">DNS activity</h2>
        <div className="flex rounded-md border border-border bg-surface p-0.5">
          {WINDOWS.map((w) => (
            <button
              key={w.minutes}
              onClick={() => onMinutesChange(w.minutes)}
              className={`rounded px-2 py-1 text-xs font-medium transition-colors ${
                minutes === w.minutes ? 'bg-accent-muted text-accent' : 'text-text-muted hover:text-text'
              }`}
            >
              {w.label}
            </button>
          ))}
        </div>
      </div>

      {isPending || !data ? (
        <div className="h-4 w-full animate-pulse rounded bg-surface-hover" />
      ) : (
        <>
          <div className="grid grid-cols-3 gap-2">
            <div className="rounded-md border border-border bg-surface p-2">
              <div className="text-lg font-semibold tabular-nums">{data.totals.queries}</div>
              <div className="text-[10px] text-text-muted">Queries</div>
            </div>
            <div className="rounded-md border border-border bg-surface p-2">
              <div className="text-lg font-semibold tabular-nums">{data.totals.blocked}</div>
              <div className="text-[10px] text-text-muted">Blocked</div>
            </div>
            <div className="rounded-md border border-border bg-surface p-2">
              <div className="text-lg font-semibold tabular-nums">{blockedPct}%</div>
              <div className="text-[10px] text-text-muted">Blocked rate</div>
            </div>
          </div>

          <div>
            <div className="mb-1 text-xs font-medium text-text-muted">Top domains</div>
            <div className="space-y-1">
              {data.topDomains.slice(0, 10).map((d) => (
                <button
                  key={d.domain}
                  onClick={() => onDomainClick(d.domain)}
                  className="group flex w-full items-center gap-2 text-left text-xs"
                >
                  <span className="w-32 shrink-0 truncate group-hover:text-accent">{d.domain}</span>
                  <span className="h-2 flex-1 overflow-hidden rounded-full bg-surface">
                    <span
                      className="block h-full rounded-full bg-accent/60"
                      style={{ width: `${(d.count / maxCount) * 100}%` }}
                    />
                  </span>
                  <span className="w-6 shrink-0 text-right text-text-muted tabular-nums">{d.count}</span>
                </button>
              ))}
              {data.topDomains.length === 0 && <div className="text-xs text-text-muted">No queries yet.</div>}
            </div>
          </div>

          {data.topBlocked.length > 0 && (
            <div>
              <div className="mb-1 text-xs font-medium text-text-muted">Top blocked</div>
              <div className="space-y-1">
                {data.topBlocked.slice(0, 5).map((d) => (
                  <div key={d.domain} className="flex items-center justify-between text-xs">
                    <span className="truncate text-danger">{d.domain}</span>
                    <span className="text-text-muted">
                      {d.count} · {d.blocklist}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div>
            <div className="mb-1 flex items-center justify-between">
              <span className="text-xs font-medium text-text-muted">Live tail</span>
              <button
                onClick={togglePause}
                className="flex items-center gap-1 rounded px-1.5 py-0.5 text-xs text-text-muted hover:text-text"
              >
                {paused ? <Play size={12} /> : <Pause size={12} />}
                {paused ? 'Resume' : 'Pause'}
              </button>
            </div>
            <div className="max-h-48 space-y-0.5 overflow-y-auto font-mono text-[11px]">
              {recent.map((q, i) => (
                <div
                  key={`${q.time}-${q.domain}-${i}`}
                  className={`flex justify-between gap-2 rounded px-1 py-0.5 ${q.action === 'block' ? 'bg-danger/10 text-danger' : ''}`}
                >
                  <span className="truncate">{q.domain}</span>
                  <span className="shrink-0 text-text-muted">{new Date(q.time).toLocaleTimeString()}</span>
                </div>
              ))}
              {recent.length === 0 && <div className="text-text-muted">No recent queries.</div>}
            </div>
          </div>
        </>
      )}
    </div>
  )
}
