import type { ClientSnapshot, ConnectionsResult, DnsResult, NetworkClient } from './types'

export class ClientsNotReadyError extends Error {
  constructor() {
    super('Snapshot not ready yet')
  }
}

export class ClientNotFoundError extends Error {
  constructor(mac: string) {
    super(`Client ${mac} not found`)
  }
}

export async function fetchClientSnapshot(): Promise<ClientSnapshot> {
  const response = await fetch('/api/clients')
  if (response.status === 503) {
    throw new ClientsNotReadyError()
  }
  if (!response.ok) {
    throw new Error(`Failed to load clients: ${response.status}`)
  }
  return (await response.json()) as ClientSnapshot
}

export async function fetchClient(mac: string): Promise<NetworkClient> {
  const response = await fetch(`/api/clients/${encodeURIComponent(mac)}`)
  if (response.status === 404) {
    throw new ClientNotFoundError(mac)
  }
  if (!response.ok) {
    throw new Error(`Failed to load client: ${response.status}`)
  }
  return (await response.json()) as NetworkClient
}

export async function fetchConnections(mac: string): Promise<ConnectionsResult> {
  const response = await fetch(`/api/clients/${encodeURIComponent(mac)}/connections`)
  if (response.status === 404) {
    throw new ClientNotFoundError(mac)
  }
  if (!response.ok) {
    throw new Error(`Failed to load connections: ${response.status}`)
  }
  return (await response.json()) as ConnectionsResult
}

export async function fetchDns(mac: string, minutes: number, tail: number): Promise<DnsResult> {
  const response = await fetch(`/api/clients/${encodeURIComponent(mac)}/dns?minutes=${minutes}&tail=${tail}`)
  if (response.status === 404) {
    throw new ClientNotFoundError(mac)
  }
  if (!response.ok) {
    throw new Error(`Failed to load DNS activity: ${response.status}`)
  }
  return (await response.json()) as DnsResult
}
