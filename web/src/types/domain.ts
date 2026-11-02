export type LoginResponse = {
  accessToken: string
  tokenType: string
  expiresAtUtc: string
}

export type RegistrationResponse = {
  requestId: string
  email: string
  status: string
  createdAtUtc: string
}

export type Wallet = {
  id: string
  userId: string
  balance: number
  currency: string
  createdAtUtc: string
  updatedAtUtc: string
}

export type WalletOperation = {
  operationId: string
  userId: string
  operationType: string
  amount: number
  status: string
  failureReason?: string | null
  createdAtUtc: string
  completedAtUtc?: string | null
}

export type Transfer = {
  id: string
  senderUserId: string
  receiverUserId: string
  amount: number
  currency: string
  status: string
  failureReason?: string | null
  createdAtUtc: string
  completedAtUtc?: string | null
  failedAtUtc?: string | null
}

export type LedgerEntry = {
  id: string
  userId: string
  referenceId: string
  entryType: string
  amount: number
  currency: string
  description?: string | null
  createdAtUtc: string
}
