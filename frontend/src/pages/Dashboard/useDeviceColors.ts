import { useRef } from 'react'

/** Distinct categorical colors for up to 8 devices, chosen for contrast against the dark map background. */
const PALETTE = [
  '#5eead4', // teal
  '#f472b6', // pink
  '#fbbf24', // amber
  '#818cf8', // indigo
  '#a3e635', // lime
  '#fb923c', // orange
  '#38bdf8', // sky
  '#e879f9', // fuchsia
]

/**
 * Assigns a stable color to each of the busiest devices, persisted for the session (a ref, not state, so
 * it survives re-renders without a re-render of its own, and a device keeps its color even if it briefly
 * drops out of the busiest set on one poll). Bounded to the palette size — beyond that, devices go grey.
 */
export function useDeviceColors() {
  const assignedRef = useRef(new Map<string, string>())

  function assign(macsBusiestFirst: string[]): Map<string, string> {
    const assigned = assignedRef.current
    for (const mac of macsBusiestFirst) {
      if (assigned.has(mac)) continue
      if (assigned.size >= PALETTE.length) continue
      const usedColors = new Set(assigned.values())
      const color = PALETTE.find((c) => !usedColors.has(c))
      if (color) assigned.set(mac, color)
    }
    return assigned
  }

  function colorFor(mac: string): string | undefined {
    return assignedRef.current.get(mac)
  }

  return { assign, colorFor }
}
