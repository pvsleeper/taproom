import type { DashboardSummaryResult, DashboardWanResult } from '../../api/types'

interface TilesProps {
  summary: DashboardSummaryResult | undefined
  wan: DashboardWanResult | undefined
}

function formatMbps(bps: number): string {
  const mbps = bps / 1_000_000
  return mbps < 0.1 ? '<0.1' : mbps < 10 ? mbps.toFixed(1) : Math.round(mbps).toString()
}

export function Tiles({ summary, wan }: TilesProps) {
  const sourcesOk = summary ? Object.values(summary.sources).every((s) => s.ok) && (wan?.ok ?? true) : true

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      <div className="rounded-lg border border-border bg-surface p-4">
        <div className="text-2xl font-semibold tabular-nums">{summary?.clients.online ?? '–'}</div>
        <div className="text-xs text-text-muted">
          Clients online
          {summary && ` · ${summary.clients.wireless} wireless, ${summary.clients.wired} wired`}
        </div>
      </div>

      <div className="rounded-lg border border-border bg-surface p-4">
        <div className="text-2xl font-semibold tabular-nums text-accent">↓ {wan ? formatMbps(wan.downBps) : '–'}</div>
        <div className="text-xs text-text-muted">
          Internet Mbps
          {wan && ` · ↑ ${formatMbps(wan.upBps)}`}
        </div>
      </div>

      <div className="rounded-lg border border-border bg-surface p-4">
        <div className="text-2xl font-semibold tabular-nums">{summary?.dns.queries ?? '–'}</div>
        <div className="text-xs text-text-muted">
          DNS queries (1h)
          {summary && ` · ${summary.dns.blocked} blocked, ${summary.dns.failed} failed`}
        </div>
      </div>

      <div className="rounded-lg border border-border bg-surface p-4">
        <div className={`text-2xl font-semibold ${sourcesOk ? 'text-accent' : 'text-warn'}`}>
          {sourcesOk ? 'OK' : 'Degraded'}
        </div>
        <div className="text-xs text-text-muted">Sources</div>
      </div>
    </div>
  )
}
