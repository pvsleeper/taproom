import * as d3 from 'd3-geo'
import { select } from 'd3-selection'
import 'd3-transition'
import { zoom, zoomIdentity, type ZoomBehavior } from 'd3-zoom'
import { Maximize2 } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { feature } from 'topojson-client'
import type { GeometryCollection, Topology } from 'topojson-specification'
import worldTopology from 'world-atlas/countries-110m.json'
import type { ConnectionMarker, HomeLocation } from '../../api/types'
import { formatBytes } from '../../lib/format'

const WIDTH = 960
const HEIGHT_DESKTOP = 480
const HEIGHT_MOBILE = 320
const GREY = '#5b6478'
// Above this many connections, arcs are drawn per-marker-per-device instead of per-connection, to keep the map responsive.
const PER_CONNECTION_ARC_LIMIT = 400

interface Tooltip {
  x: number
  y: number
  lines: { text: string; color?: string }[]
}

interface NetworkMapProps {
  markers: ConnectionMarker[]
  connectionCount: number
  home: HomeLocation | null
  colorFor: (mac: string) => string | undefined
  soloMac: string | null
  isLoading: boolean
  geoIpOk: boolean
}

export function NetworkMap({ markers, connectionCount, home, colorFor, soloMac, isLoading, geoIpOk }: NetworkMapProps) {
  const svgRef = useRef<SVGSVGElement>(null)
  const zoomGroupRef = useRef<SVGGElement>(null)
  const zoomBehaviorRef = useRef<ZoomBehavior<SVGSVGElement, unknown> | null>(null)
  const [tooltip, setTooltip] = useState<Tooltip | null>(null)
  const [height, setHeight] = useState(HEIGHT_DESKTOP)

  useEffect(() => {
    function onResize() {
      setHeight(window.innerWidth < 640 ? HEIGHT_MOBILE : HEIGHT_DESKTOP)
    }
    onResize()
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  const projection = useMemo(() => d3.geoEqualEarth().rotate([-150, 0]).fitSize([WIDTH, height], { type: 'Sphere' }), [height])
  const path = useMemo(() => d3.geoPath(projection), [projection])

  const countries = useMemo(() => {
    const topo = worldTopology as unknown as Topology
    const geo = feature(topo, topo.objects.countries as GeometryCollection)
    return geo.features
  }, [])

  const maxBytes = useMemo(() => Math.max(1, ...markers.map((m) => m.bytes)), [markers])
  const maxRate = useMemo(() => Math.max(1, ...markers.flatMap((m) => m.devices.map((d) => d.downBps + d.upBps))), [markers])

  function markerRadius(bytes: number): number {
    return 4 + 14 * Math.sqrt(bytes / maxBytes)
  }

  function arcWidth(rateBps: number): number {
    return 0.75 + 3 * Math.sqrt(rateBps / maxRate)
  }

  function dashDuration(rateBps: number): number {
    const intensity = Math.sqrt(rateBps / maxRate)
    return 3 - 2.4 * intensity
  }

  function formatMbps(bps: number): string {
    const mbps = bps / 1_000_000
    return mbps < 0.1 ? '<0.1 Mbps' : `${mbps < 10 ? mbps.toFixed(1) : Math.round(mbps)} Mbps`
  }

  useEffect(() => {
    if (!svgRef.current || !zoomGroupRef.current) return
    const svg = select(svgRef.current)
    const g = select(zoomGroupRef.current)

    const behavior = zoom<SVGSVGElement, unknown>()
      .scaleExtent([1, 8])
      .on('zoom', (event) => {
        g.attr('transform', event.transform.toString())
      })

    svg.call(behavior)
    zoomBehaviorRef.current = behavior

    return () => {
      svg.on('.zoom', null)
    }
  }, [])

  function fitConnections() {
    if (!svgRef.current || !zoomBehaviorRef.current || markers.length === 0) {
      resetZoom()
      return
    }

    const points = markers.map((m) => projection([m.lon, m.lat])).filter((p): p is [number, number] => p !== null)
    if (home) {
      const homePoint = projection([home.lon, home.lat])
      if (homePoint) points.push(homePoint)
    }
    if (points.length === 0) return

    const xs = points.map((p) => p[0])
    const ys = points.map((p) => p[1])
    const [minX, maxX] = [Math.min(...xs), Math.max(...xs)]
    const [minY, maxY] = [Math.min(...ys), Math.max(...ys)]
    const boxWidth = Math.max(1, maxX - minX)
    const boxHeight = Math.max(1, maxY - minY)
    const scale = Math.min(8, Math.max(1, 0.8 / Math.max(boxWidth / WIDTH, boxHeight / height)))
    const cx = (minX + maxX) / 2
    const cy = (minY + maxY) / 2

    const svg = select(svgRef.current)
    svg
      .transition()
      .duration(500)
      .call(zoomBehaviorRef.current.transform, zoomIdentity.translate(WIDTH / 2, height / 2).scale(scale).translate(-cx, -cy))
  }

  function resetZoom() {
    if (!svgRef.current || !zoomBehaviorRef.current) return
    select(svgRef.current).transition().duration(400).call(zoomBehaviorRef.current.transform, zoomIdentity)
  }

  const homePoint = home ? projection([home.lon, home.lat]) : null
  const perConnectionArcs = connectionCount <= PER_CONNECTION_ARC_LIMIT

  return (
    <div className="relative overflow-hidden rounded-lg border border-border bg-[#060b16]">
      <svg
        ref={svgRef}
        viewBox={`0 0 ${WIDTH} ${height}`}
        className="block w-full cursor-grab active:cursor-grabbing"
        style={{ height }}
        onMouseLeave={() => setTooltip(null)}
      >
        <defs>
          <radialGradient id="home-pulse" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="var(--color-accent)" stopOpacity="0.6" />
            <stop offset="100%" stopColor="var(--color-accent)" stopOpacity="0" />
          </radialGradient>
        </defs>

        <rect x={0} y={0} width={WIDTH} height={height} fill="#060b16" />

        <g ref={zoomGroupRef}>
          <path d={path({ type: 'Sphere' }) ?? undefined} fill="#0b1120" />
          {countries.map((c, i) => (
            <path key={i} d={path(c) ?? undefined} fill="#141d33" stroke="#22304a" strokeWidth={0.5} />
          ))}

          {/* Arcs: home to each marker, one per device (or one aggregate per marker above the perf threshold) */}
          {homePoint &&
            markers.flatMap((m) => {
              const target = projection([m.lon, m.lat])
              if (!target) return []
              const arcPath = path({ type: 'LineString', coordinates: [[home!.lon, home!.lat], [m.lon, m.lat]] })

              const arcsForMarker = perConnectionArcs
                ? m.devices
                : [
                    {
                      mac: m.devices[0]?.mac ?? m.key,
                      name: m.label,
                      connectionCount: m.connectionCount,
                      downBps: m.downBps,
                      upBps: m.upBps,
                    },
                  ]

              return arcsForMarker.map((device) => {
                const color = colorFor(device.mac) ?? GREY
                const dimmed = soloMac !== null && soloMac !== device.mac
                const rate = device.downBps + device.upBps

                return (
                  <path
                    key={`${m.key}:${device.mac}`}
                    d={arcPath ?? undefined}
                    fill="none"
                    stroke={color}
                    strokeWidth={arcWidth(rate)}
                    strokeOpacity={dimmed ? 0.1 : 0.65}
                    strokeDasharray="5 5"
                    className="animate-dash-flow"
                    style={{ animationDuration: `${dashDuration(rate)}s` }}
                  />
                )
              })
            })}

          {homePoint && (
            <g transform={`translate(${homePoint[0]}, ${homePoint[1]})`}>
              <circle r={14} fill="url(#home-pulse)" className="animate-pulse" />
              <circle r={4} fill="var(--color-accent)" stroke="#060b16" strokeWidth={1.5} />
            </g>
          )}

          {markers.map((m) => {
            const point = projection([m.lon, m.lat])
            if (!point) return null
            const dimmed = soloMac !== null && !m.devices.some((d) => d.mac === soloMac)
            const dominant = m.devices.reduce((a, b) => (a.downBps + a.upBps >= b.downBps + b.upBps ? a : b), m.devices[0])
            const color = dominant ? (colorFor(dominant.mac) ?? GREY) : GREY

            return (
              <g
                key={m.key}
                transform={`translate(${point[0]}, ${point[1]})`}
                opacity={dimmed ? 0.2 : 1}
                onMouseEnter={(e) => {
                  const rect = svgRef.current?.getBoundingClientRect()
                  setTooltip({
                    x: e.clientX - (rect?.left ?? 0),
                    y: e.clientY - (rect?.top ?? 0),
                    lines: [
                      { text: m.label },
                      { text: `${m.connectionCount} connection${m.connectionCount === 1 ? '' : 's'} · ${formatBytes(m.bytes)}` },
                      ...m.devices
                        .slice()
                        .sort((a, b) => b.downBps + b.upBps - (a.downBps + a.upBps))
                        .map((d) => ({
                          text: `${d.name} · ↓ ${formatMbps(d.downBps)} ↑ ${formatMbps(d.upBps)}`,
                          color: colorFor(d.mac) ?? GREY,
                        })),
                    ],
                  })
                }}
                onMouseLeave={() => setTooltip(null)}
              >
                <circle r={markerRadius(m.bytes)} fill={color} fillOpacity={0.25} stroke={color} strokeWidth={1.5} />
                {m.connectionCount > 1 && (
                  <text textAnchor="middle" dy={4} fontSize={10} fill="white" fontWeight={600}>
                    {m.connectionCount}
                  </text>
                )}
              </g>
            )
          })}
        </g>
      </svg>

      {tooltip && (
        <div
          className="pointer-events-none absolute z-10 max-w-xs rounded-md border border-border bg-surface px-2 py-1.5 text-xs shadow-lg"
          style={{ left: tooltip.x + 12, top: tooltip.y + 12 }}
        >
          {tooltip.lines.map((line, i) => (
            <div key={i} className={i === 0 ? 'font-medium' : 'flex items-center gap-1.5 text-text-muted'}>
              {line.color && <span className="h-1.5 w-1.5 shrink-0 rounded-full" style={{ background: line.color }} />}
              {line.text}
            </div>
          ))}
        </div>
      )}

      <div className="absolute top-2 right-2 flex items-center gap-2">
        <button
          onClick={fitConnections}
          className="flex items-center gap-1 rounded-full border border-border bg-surface/80 px-2.5 py-1 text-xs text-text-muted backdrop-blur hover:text-text"
        >
          <Maximize2 size={12} />
          Fit connections
        </button>
      </div>

      {isLoading && markers.length === 0 && (
        <div className="absolute inset-0 flex items-center justify-center text-sm text-text-muted">Loading connections…</div>
      )}

      {!geoIpOk && (
        <div className="absolute bottom-2 left-2 rounded-full border border-warn/30 bg-warn/10 px-2.5 py-1 text-xs text-warn backdrop-blur">
          GeoIP unavailable — locations and the map are empty
        </div>
      )}
    </div>
  )
}
