import { apiClient } from '../client'
import type { LoginResponse, RegistrationResponse } from '../../types/domain'

export type Credentials = { email: string; password: string }

export async function login(credentials: Credentials) {
  const response = await apiClient.post<LoginResponse>('/api/identity/login', credentials)
  return response.data
}

export async function register(credentials: Credentials) {
  const response = await apiClient.post<RegistrationResponse>('/api/identity/register', credentials)
  return response.data
}

export async function getRegistrationStatus(requestId: string) {
  const response = await apiClient.get<RegistrationResponse>(`/api/identity/registration-status/${requestId}`)
  return response.data
}
