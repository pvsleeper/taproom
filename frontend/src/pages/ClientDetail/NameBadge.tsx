import type { NameSource } from '../../api/types'

const LABEL: Record<NameSource, string> = { Dns: 'DNS', Ptr: 'PTR', Asn: 'ASN', Ip: 'IP' }

export function NameBadge({ source }: { source: NameSource }) {
  const confident = source === 'Dns'
  return (
    <span
      className={`rounded px-1 py-0.5 text-[10px] font-medium ${
        confident ? 'bg-accent-muted text-accent' : 'bg-warn/10 text-warn'
      }`}
    >
      {LABEL[source]}
    </span>
  )
}
