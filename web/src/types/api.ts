export type ServiceStatus = 'checking' | 'healthy' | 'offline'

export type ApiService = {
  name: string
  path: string
}

export type ServiceHealth = {
  service: string
  status: string
}
