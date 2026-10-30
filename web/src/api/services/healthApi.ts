import { apiClient } from '../client'
import type { ApiService, ServiceHealth } from '../../types/api'

export const apiServices: ApiService[] = [
  { name: 'Identity', path: '/api/identity/status' },
  { name: 'Wallet', path: '/api/wallet/status' },
  { name: 'Transfer', path: '/api/transfer/status' },
  { name: 'Ledger', path: '/api/ledger/status' },
]

export async function getServiceHealth(path: string) {
  const response = await apiClient.get<ServiceHealth>(path)
  return response.data
}
