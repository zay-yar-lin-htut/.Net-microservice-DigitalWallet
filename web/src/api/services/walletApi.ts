import { apiClient } from '../client'
import type { Wallet } from '../../types/domain'

export async function getWallet() {
  const response = await apiClient.get<Wallet>('/api/wallet/me')
  return response.data
}

export async function createWallet() {
  const response = await apiClient.post<Wallet>('/api/wallet')
  return response.data
}

export async function deposit(amount: number) {
  const response = await apiClient.post<Wallet>('/api/wallet/deposit', { amount })
  return response.data
}

export async function withdraw(amount: number) {
  const response = await apiClient.post<Wallet>('/api/wallet/withdraw', { amount })
  return response.data
}
