import { apiClient } from '../client'
import type { Wallet, WalletOperation } from '../../types/domain'

type WalletPayload = Partial<Wallet> & {
  Balance?: number | string
  Currency?: string
}

function normalizeWallet(payload: WalletPayload): Wallet {
  return {
    ...payload,
    id: payload.id ?? '',
    userId: payload.userId ?? '',
    balance: Number(payload.balance ?? payload.Balance ?? 0),
    currency: payload.currency ?? payload.Currency ?? 'MMK',
    createdAtUtc: payload.createdAtUtc ?? '',
    updatedAtUtc: payload.updatedAtUtc ?? '',
  }
}

export async function getWallet() {
  const response = await apiClient.get<WalletPayload>('/api/wallet/me')
  return normalizeWallet(response.data)
}

export async function createWallet() {
  const response = await apiClient.post<WalletPayload>('/api/wallet')
  return normalizeWallet(response.data)
}

export async function deposit(amount: number) {
  const response = await apiClient.post<WalletOperation>('/api/wallet/deposit', { amount })
  return response.data
}

export async function withdraw(amount: number) {
  const response = await apiClient.post<WalletOperation>('/api/wallet/withdraw', { amount })
  return response.data
}

export async function getWalletOperation(operationId: string) {
  const response = await apiClient.get<WalletOperation>(`/api/wallet/operations/${operationId}`)
  return response.data
}
