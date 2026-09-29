import { useQuery } from '@tanstack/react-query'
import { fetchBandwidth, fetchClient, fetchConnections, fetchDns } from './client'

export function useClient(mac: string) {
  return useQuery({
    queryKey: ['client', mac],
    queryFn: () => fetchClient(mac),
    refetchInterval: 30_000,
    refetchIntervalInBackground: false,
    retry: 1,
  })
}

export function useConnections(mac: string) {
  return useQuery({
    queryKey: ['connections', mac],
    queryFn: () => fetchConnections(mac),
    refetchInterval: 5_000,
    refetchIntervalInBackground: false,
    retry: 1,
  })
}

export function useDns(mac: string, minutes: number, tail: number) {
  return useQuery({
    queryKey: ['dns', mac, minutes, tail],
    queryFn: () => fetchDns(mac, minutes, tail),
    refetchInterval: 10_000,
    refetchIntervalInBackground: false,
    retry: 1,
  })
}

export function useBandwidth(mac: string) {
  return useQuery({
    queryKey: ['bandwidth', mac],
    queryFn: () => fetchBandwidth(mac),
    refetchInterval: 2_000,
    refetchIntervalInBackground: false,
    retry: 1,
  })
}
