import type { NetworkClient } from '../../api/types'

interface SummaryCardsProps {
  clients: NetworkClient[]
}

export function SummaryCards({ clients }: SummaryCardsProps) {
  const total = clients.length
  const online = clients.filter((c) => c.online).length
  const wireless = clients.filter((c) => c.connection === 'Wireless').length
  const wired = clients.filter((c) => c.connection === 'Wired').length

  const cards = [
    { label: 'Total clients', value: total },
    { label: 'Online', value: online },
    { label: 'Wireless', value: wireless },
    { label: 'Wired', value: wired },
  ]

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {cards.map((card) => (
        <div key={card.label} className="rounded-lg border border-border bg-surface p-4">
          <div className="text-2xl font-semibold tabular-nums">{card.value}</div>
          <div className="text-xs text-text-muted">{card.label}</div>
        </div>
      ))}
    </div>
  )
}
