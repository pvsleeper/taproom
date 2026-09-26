import type { ClientSnapshot } from './types'

export class ClientsNotReadyError extends Error {
  constructor() {
    super('Snapshot not ready yet')
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
