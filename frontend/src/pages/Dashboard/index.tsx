import { useState } from 'react'
import { useDashboardConnections, useDashboardSummary, useDashboardTopTalkers, useDashboardWan } from '../../api/useDashboard'
import { BandwidthCard } from '../ClientDetail/BandwidthCard'
import { MapLegend } from './MapLegend'
import { NetworkMap } from './NetworkMap'
import { Tiles } from './Tiles'
import { TopTalkers } from './TopTalkers'
import { useDeviceColors } from './useDeviceColors'
import { useWanBuffer } from './useWanBuffer'

export function DashboardPage() {
  const summary = useDashboardSummary()
  const wan = useDashboardWan()
  const connections = useDashboardConnections()
  const topTalkers = useDashboardTopTalkers(5)
  const wanBuffer = useWanBuffer()
  const { assign, colorFor } = useDeviceColors()
  const [soloMac, setSoloMac] = useState<string | null>(null)

  const busiestMacs = (topTalkers.data?.devices ?? []).map((d) => d.mac)
  assign(busiestMacs)

  function toggleSolo(mac: string) {
    setSoloMac((current) => (current === mac ? null : mac))
  }

  const geoIpOk = connections.data?.sources.geoip?.ok ?? true

  return (
    <div className="flex h-full flex-col">
      <div className="border-b border-border px-6 py-4">
        <h1 className="text-xl font-semibold">Dashboard</h1>
      </div>

      <div className="flex-1 overflow-y-auto p-6">
        <div className="flex flex-col gap-4">
          <Tiles summary={summary.data} wan={wan.data} />

          <div className="flex flex-col gap-2">
            <NetworkMap
              markers={connections.data?.markers ?? []}
              connectionCount={connections.data?.connections.length ?? 0}
              home={connections.data?.home ?? null}
              colorFor={colorFor}
              soloMac={soloMac}
              isLoading={connections.isPending}
              geoIpOk={geoIpOk}
            />
            <MapLegend
              devices={topTalkers.data?.devices ?? []}
              colorFor={colorFor}
              soloMac={soloMac}
              onToggleSolo={toggleSolo}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
            <div className="lg:col-span-2">
              <BandwidthCard
                points={wanBuffer.points}
                currentDownBps={wanBuffer.current?.downBps}
                currentUpBps={wanBuffer.current?.upBps}
                sourceOk={wanBuffer.current?.ok ?? true}
              />
            </div>
            <TopTalkers devices={topTalkers.data?.devices} isPending={topTalkers.isPending} colorFor={colorFor} />
          </div>
        </div>
      </div>
    </div>
  )
}
