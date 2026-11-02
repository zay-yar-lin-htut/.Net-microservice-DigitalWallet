import { apiClient } from '../client'
import type { LedgerEntry } from '../../types/domain'
import type { PagedResult } from '../../types/pagination'

export async function getLedgerEntries(page = 1, pageSize = 5) {
  const response = await apiClient.get<PagedResult<LedgerEntry>>('/api/ledger/me', {
    params: { page, pageSize },
  })
  return response.data
}
