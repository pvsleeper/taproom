import { useQuery } from '@tanstack/react-query'
import { fetchClientSnapshot } from './client'

const REFETCH_INTERVAL_MS = 30_000

export function useClientSnapshot() {
  return useQuery({
    queryKey: ['clients'],
    queryFn: fetchClientSnapshot,
    refetchInterval: REFETCH_INTERVAL_MS,
    retry: 1,
  })
}
