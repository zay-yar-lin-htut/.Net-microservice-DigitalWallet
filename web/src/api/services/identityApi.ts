import { apiClient } from '../client'
import type { LoginResponse } from '../../types/domain'

export type Credentials = { email: string; password: string }

export async function login(credentials: Credentials) {
  const response = await apiClient.post<LoginResponse>('/api/identity/login', credentials)
  return response.data
}

export async function register(credentials: Credentials) {
  await apiClient.post('/api/identity/register', credentials)
}
