import { apiClient } from '../client'
import type { LedgerEntry } from '../../types/domain'

export async function getLedgerEntries() {
  const response = await apiClient.get<LedgerEntry[]>('/api/ledger/me')
  return response.data
}
