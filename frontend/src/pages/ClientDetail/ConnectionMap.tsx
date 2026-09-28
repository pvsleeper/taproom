import * as d3 from 'd3-geo'
import { select } from 'd3-selection'
import 'd3-transition'
import { zoom, zoomIdentity, type ZoomBehavior } from 'd3-zoom'
import { Maximize2 } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { feature } from 'topojson-client'
import type { GeometryCollection, Topology } from 'topojson-specification'
import worldTopology from 'world-atlas/countries-110m.json'
import type { ConnectionMarker, EnrichedConnection, HomeLocation } from '../../api/types'
import { formatBytes } from '../../lib/format'

const WIDTH = 960
const HEIGHT_DESKTOP = 420
const HEIGHT_MOBILE = 280

interface Tooltip {
  x: number
  y: number
  lines: string[]
}

interface ConnectionMapProps {
  connections: EnrichedConnection[]
  markers: ConnectionMarker[]
  home: HomeLocation | null
  hoveredRemote: string | null
  highlightedDomain: string | null
  locationFilterKey: string | null
  onMarkerClick: (key: string | null) => void
  isLoading: boolean
  geoIpOk: boolean
}

export function ConnectionMap({
  connections,
  markers,
  home,
  hoveredRemote,
  highlightedDomain,
  locationFilterKey,
  onMarkerClick,
  isLoading,
  geoIpOk,
}: ConnectionMapProps) {
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

  const projection = useMemo(
    () =>
      d3
        .geoEqualEarth()
        .rotate([-150, 0])
        .fitSize([WIDTH, height], { type: 'Sphere' }),
    [height],
  )

  const path = useMemo(() => d3.geoPath(projection), [projection])

  const countries = useMemo(() => {
    const topo = worldTopology as unknown as Topology
    const geo = feature(topo, topo.objects.countries as GeometryCollection)
    return geo.features
  }, [])

  const maxBytes = useMemo(() => Math.max(1, ...markers.map((m) => m.bytes)), [markers])

  function markerRadius(bytes: number): number {
    return 4 + 14 * Math.sqrt(bytes / maxBytes)
  }

  function connectionsForMarker(marker: ConnectionMarker): EnrichedConnection[] {
    return connections.filter((c) => {
      if (c.lat === null || c.lon === null) return false
      const key = `${Math.round(c.lat * 10) / 10},${Math.round(c.lon * 10) / 10}`
      return key === marker.key
    })
  }

  // Set up zoom behavior once.
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
      .call(
        zoomBehaviorRef.current.transform,
        zoomIdentity.translate(WIDTH / 2, height / 2).scale(scale).translate(-cx, -cy),
      )
  }

  function resetZoom() {
    if (!svgRef.current || !zoomBehaviorRef.current) return
    select(svgRef.current).transition().duration(400).call(zoomBehaviorRef.current.transform, zoomIdentity)
  }

  const homePoint = home ? projection([home.lon, home.lat]) : null

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

          {/* Arcs: home to each marker */}
          {homePoint &&
            markers.map((m) => {
              const target = projection([m.lon, m.lat])
              if (!target) return null
              const arcPath = path({
                type: 'LineString',
                coordinates: [
                  [home!.lon, home!.lat],
                  [m.lon, m.lat],
                ],
              })
              const memberConns = connectionsForMarker(m)
              const isDns = memberConns.some((c) => c.nameSource === 'Dns')
              const isHighlighted =
                (hoveredRemote && memberConns.some((c) => `${c.remoteIp}:${c.remotePort}` === hoveredRemote)) ||
                (highlightedDomain && memberConns.some((c) => c.name === highlightedDomain || c.otherNames.includes(highlightedDomain)))
              const dimmed = locationFilterKey !== null && locationFilterKey !== m.key

              return (
                <path
                  key={m.key}
                  d={arcPath ?? undefined}
                  fill="none"
                  stroke={isDns ? 'var(--color-accent)' : 'var(--color-warn)'}
                  strokeWidth={isHighlighted ? 2.5 : Math.max(0.75, Math.sqrt(m.bytes / maxBytes) * 2)}
                  strokeOpacity={dimmed ? 0.15 : isHighlighted ? 1 : 0.55}
                />
              )
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
            const dimmed = locationFilterKey !== null && locationFilterKey !== m.key
            const memberConns = connectionsForMarker(m)
            const isDns = memberConns.some((c) => c.nameSource === 'Dns')

            return (
              <g
                key={m.key}
                transform={`translate(${point[0]}, ${point[1]})`}
                className="cursor-pointer"
                opacity={dimmed ? 0.25 : 1}
                onClick={() => onMarkerClick(locationFilterKey === m.key ? null : m.key)}
                onMouseEnter={(e) => {
                  const rect = svgRef.current?.getBoundingClientRect()
                  setTooltip({
                    x: e.clientX - (rect?.left ?? 0),
                    y: e.clientY - (rect?.top ?? 0),
                    lines: [
                      m.label,
                      `${m.connectionCount} connection${m.connectionCount === 1 ? '' : 's'}`,
                      formatBytes(m.bytes),
                    ],
                  })
                }}
                onMouseLeave={() => setTooltip(null)}
              >
                <circle
                  r={markerRadius(m.bytes)}
                  fill={isDns ? 'var(--color-accent)' : 'var(--color-warn)'}
                  fillOpacity={0.25}
                  stroke={isDns ? 'var(--color-accent)' : 'var(--color-warn)'}
                  strokeWidth={1.5}
                />
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
          className="pointer-events-none absolute z-10 rounded-md border border-border bg-surface px-2 py-1.5 text-xs shadow-lg"
          style={{ left: tooltip.x + 12, top: tooltip.y + 12 }}
        >
          {tooltip.lines.map((line, i) => (
            <div key={i} className={i === 0 ? 'font-medium' : 'text-text-muted'}>
              {line}
            </div>
          ))}
        </div>
      )}

      <div className="absolute top-2 right-2 flex items-center gap-2">
        <div className="flex items-center gap-1.5 rounded-full border border-border bg-surface/80 px-2 py-1 text-[10px] text-text-muted backdrop-blur">
          <span className="h-1.5 w-1.5 rounded-full bg-accent" /> DNS-named
          <span className="ml-1.5 h-1.5 w-1.5 rounded-full bg-warn" /> PTR/ASN/IP
        </div>
        <button
          onClick={fitConnections}
          className="flex items-center gap-1 rounded-full border border-border bg-surface/80 px-2.5 py-1 text-xs text-text-muted backdrop-blur hover:text-text"
        >
          <Maximize2 size={12} />
          Fit connections
        </button>
      </div>

      {isLoading && connections.length === 0 && (
        <div className="absolute inset-0 flex items-center justify-center text-sm text-text-muted">
          Loading connections…
        </div>
      )}

      {!geoIpOk && (
        <div className="absolute bottom-2 left-2 rounded-full border border-warn/30 bg-warn/10 px-2.5 py-1 text-xs text-warn backdrop-blur">
          GeoIP unavailable — locations and the map are empty
        </div>
      )}
    </div>
  )
}
