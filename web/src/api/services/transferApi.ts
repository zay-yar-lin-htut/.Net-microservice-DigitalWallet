import { apiClient } from '../client'
import type { Transfer } from '../../types/domain'
import type { PagedResult } from '../../types/pagination'

export type CreateTransfer = {
  receiverUserId: string
  amount: number
  currency: string
}

export async function getTransfers(page = 1, pageSize = 5) {
  const response = await apiClient.get<PagedResult<Transfer>>('/api/transfer/me', {
    params: { page, pageSize },
  })
  return response.data
}

export async function getTransfer(transferId: string) {
  const response = await apiClient.get<Transfer>(`/api/transfer/${transferId}`)
  return response.data
}

export async function createTransfer(
  request: CreateTransfer,
  idempotencyKey = crypto.randomUUID(),
) {
  const response = await apiClient.post<Transfer>('/api/transfer', request, {
    headers: { 'Idempotency-Key': idempotencyKey },
  })
  return response.data
}
