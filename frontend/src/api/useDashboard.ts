import { useQuery } from '@tanstack/react-query'
import { fetchDashboardConnections, fetchDashboardSummary, fetchDashboardTopTalkers, fetchDashboardWan } from './client'

export function useDashboardSummary() {
  return useQuery({
    queryKey: ['dashboard', 'summary'],
    queryFn: fetchDashboardSummary,
    refetchInterval: 10_000,
    refetchIntervalInBackground: false,
    retry: 1,
  })
}

export function useDashboardWan() {
  return useQuery({
    queryKey: ['dashboard', 'wan'],
    queryFn: fetchDashboardWan,
    refetchInterval: 2_000,
    refetchIntervalInBackground: false,
    retry: 1,
  })
}

export function useDashboardConnections() {
  return useQuery({
    queryKey: ['dashboard', 'connections'],
    queryFn: fetchDashboardConnections,
    refetchInterval: 5_000,
    refetchIntervalInBackground: false,
    retry: 1,
  })
}

export function useDashboardTopTalkers(limit: number) {
  return useQuery({
    queryKey: ['dashboard', 'top-talkers', limit],
    queryFn: () => fetchDashboardTopTalkers(limit),
    refetchInterval: 3_000,
    refetchIntervalInBackground: false,
    retry: 1,
  })
}
