import type { ServiceStatus } from '../types/api'

type ServiceStatusCardProps = {
  name: string
  status: ServiceStatus
}

export function ServiceStatusCard({ name, status }: ServiceStatusCardProps) {
  const indicator = {
    healthy: 'bg-emerald-400',
    offline: 'bg-rose-400',
    checking: 'animate-pulse bg-amber-400',
  }[status]

  return (
    <article className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
      <div className="flex items-center justify-between">
        <h2 className="font-semibold">{name} Service</h2>
        <span className={`h-3 w-3 rounded-full ${indicator}`} />
      </div>
      <p className="mt-3 text-sm capitalize text-slate-400">{status}</p>
    </article>
  )
}
