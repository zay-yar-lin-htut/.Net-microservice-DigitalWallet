import { apiClient } from '../client'
import type { Transfer } from '../../types/domain'

export type CreateTransfer = {
  receiverUserId: string
  amount: number
  currency: string
}

export async function getTransfers() {
  const response = await apiClient.get<Transfer[]>('/api/transfer/me')
  return response.data
}

export async function createTransfer(request: CreateTransfer) {
  const response = await apiClient.post<Transfer>('/api/transfer', request)
  return response.data
}
