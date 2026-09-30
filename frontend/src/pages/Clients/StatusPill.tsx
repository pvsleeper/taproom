import type { SourceStatus } from '../../api/types'

interface StatusPillProps {
  name: string
  status: SourceStatus
}

export function StatusPill({ name, status }: StatusPillProps) {
  return (
    <div
      className={`
        group relative flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium
        ${status.ok ? 'border-online/30 bg-online/10 text-online' : 'border-warn/30 bg-warn/10 text-warn'}
      `}
    >
      <span className={`h-1.5 w-1.5 rounded-full ${status.ok ? 'bg-online' : 'bg-warn'}`} />
      <span className="capitalize">{name}</span>
      {!status.ok && status.error && (
        <div className="pointer-events-none absolute top-full right-0 z-10 mt-1.5 hidden w-56 rounded-md border border-border bg-surface p-2 text-xs text-text-muted shadow-lg group-hover:block">
          {status.error}
        </div>
      )}
    </div>
  )
}
