import { useState, useEffect, useCallback } from 'react'

interface WoJob {
  id: number
  stJobId: number
  jobNumber: string
  customerName: string
  status: string
  jobTypeName: string | null
  totalAmount: number
  createdOn: string | null
  daysSince: number
}

interface WoColumn {
  status: string
  label: string
  color: string
  count: number
  jobs: WoJob[]
}

interface BoardData {
  totalJobs: number
  totalAmount: number
  columns: WoColumn[]
}

function fmt(n: number) {
  if (n <= 0) return ''
  return n.toLocaleString('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 0, maximumFractionDigits: 0 })
}

function colorMap(color: string) {
  switch (color) {
    case 'info': return { border: 'border-t-info', badge: 'badge-info', text: 'text-info' }
    case 'primary': return { border: 'border-t-primary', badge: 'badge-primary', text: 'text-primary' }
    case 'warning': return { border: 'border-t-warning', badge: 'badge-warning', text: 'text-warning' }
    case 'error': return { border: 'border-t-error', badge: 'badge-error', text: 'text-error' }
    default: return { border: 'border-t-base-content/20', badge: 'badge-ghost', text: 'text-base-content' }
  }
}

export function WoBoardPage() {
  const [data, setData] = useState<BoardData | null>(null)
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')

  const fetchData = useCallback(async () => {
    const res = await fetch('/api/wo-board', { credentials: 'include' })
    if (res.ok) setData(await res.json())
  }, [])

  useEffect(() => { fetchData().finally(() => setLoading(false)) }, [fetchData])

  if (loading) {
    return <div className="flex items-center justify-center py-20"><span className="loading loading-spinner loading-md text-primary" /></div>
  }

  if (!data) {
    return <div className="text-center py-20 text-base-content/40">Failed to load work orders.</div>
  }

  const q = search.toLowerCase()

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-base-content">Work Order Board</h1>
          <p className="text-sm text-base-content/60 mt-0.5">
            {data.totalJobs} open work orders {data.totalAmount > 0 && ` \u00b7 ${fmt(data.totalAmount)} total`}
          </p>
        </div>
        <div className="input input-sm max-w-xs">
          <span className="icon-[tabler--search] text-base-content/40 size-4 shrink-0" />
          <input
            type="search"
            placeholder="Search jobs..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="grow"
          />
        </div>
      </div>

      {/* Kanban columns */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4 items-start">
        {data.columns.map(col => {
          const colors = colorMap(col.color)
          const filteredJobs = q
            ? col.jobs.filter(j => j.customerName.toLowerCase().includes(q) || j.jobNumber.includes(q))
            : col.jobs

          return (
            <div key={col.status} className={`rounded-box border border-base-content/10 bg-base-100 border-t-[3px] ${colors.border} flex flex-col`}>
              {/* Column header */}
              <div className="flex items-center justify-between px-4 py-3 border-b border-base-content/10">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-sm text-base-content">{col.label}</h3>
                  <span className={`badge badge-sm ${colors.badge}`}>{filteredJobs.length}</span>
                </div>
              </div>

              {/* Cards */}
              <div className="p-2 space-y-2 max-h-[calc(100vh-16rem)] overflow-y-auto">
                {filteredJobs.length === 0 ? (
                  <div className="text-center py-6 text-base-content/30 text-xs">
                    {q ? 'No matches' : 'No work orders'}
                  </div>
                ) : (
                  filteredJobs.map(job => (
                    <div
                      key={job.id}
                      className="rounded-lg border border-base-content/10 bg-base-200/30 p-3 hover:border-base-content/20 transition-colors"
                    >
                      <div className="flex items-start justify-between gap-2 mb-1.5">
                        <span className="text-sm font-medium text-base-content leading-tight">{job.customerName}</span>
                        <span className="font-mono text-xs text-primary shrink-0">#{job.jobNumber}</span>
                      </div>
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          {job.jobTypeName && (
                            <span className="text-[10px] text-base-content/50 bg-base-content/5 rounded px-1.5 py-0.5">{job.jobTypeName}</span>
                          )}
                          {job.totalAmount > 0 && (
                            <span className="text-xs font-medium text-base-content/70">{fmt(job.totalAmount)}</span>
                          )}
                        </div>
                        <span className={`text-xs ${job.daysSince >= 90 ? 'text-error font-medium' : job.daysSince >= 30 ? 'text-warning' : 'text-base-content/40'}`}>
                          {job.daysSince}d
                        </span>
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
