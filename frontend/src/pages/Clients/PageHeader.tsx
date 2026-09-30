import { useEffect, useState } from 'react'
import type { ClientSnapshot } from '../../api/types'
import { formatRelativeSeconds } from '../../lib/format'
import { StatusPill } from './StatusPill'

interface PageHeaderProps {
  snapshot: ClientSnapshot | undefined
}

export function PageHeader({ snapshot }: PageHeaderProps) {
  const [, forceTick] = useState(0)

  useEffect(() => {
    const id = setInterval(() => forceTick((n) => n + 1), 1000)
    return () => clearInterval(id)
  }, [])

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-6 py-4">
      <h1 className="text-xl font-semibold">Clients</h1>
      <div className="flex items-center gap-3">
        {snapshot && (
          <span className="text-xs text-text-muted">Updated {formatRelativeSeconds(snapshot.generatedAt)}</span>
        )}
        <div className="flex items-center gap-2">
          {snapshot &&
            Object.entries(snapshot.sources).map(([name, status]) => (
              <StatusPill key={name} name={name} status={status} />
            ))}
        </div>
      </div>
    </div>
  )
}
