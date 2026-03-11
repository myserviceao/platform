import { useState, useEffect, useCallback } from 'react'

interface HoldJob {
  id: number
  stJobId: number
  jobNumber: string
  customerName: string
  jobTypeName: string | null
  holdReasonName: string | null
  totalAmount: number
  createdOn: string | null
  daysSince: number
}

interface HoldColumn {
  key: string
  label: string
  count: number
  jobs: HoldJob[]
}

interface HoldData {
  totalHolds: number
  holdReasonCount: number
  totalAmount: number
  holdReasons: string[]
  columns: HoldColumn[]
}

function fmt(n: number) {
  if (n <= 0) return ''
  return n.toLocaleString('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 0, maximumFractionDigits: 0 })
}

export function HoldBoardPage() {
  const [data, setData] = useState<HoldData | null>(null)
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')

  const fetchData = useCallback(async () => {
    const res = await fetch('/api/wo-board/holds', { credentials: 'include' })
    if (res.ok) setData(await res.json())
  }, [])

  useEffect(() => { fetchData().finally(() => setLoading(false)) }, [fetchData])

  const assignReason = async (jobId: number, reason: string) => {
    await fetch('/api/wo-board/holds/' + jobId + '/reason', {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason })
    })
    fetchData()
  }

  if (loading) {
    return <div className="flex items-center justify-center py-20"><span className="loading loading-spinner loading-md text-primary" /></div>
  }

  if (!data) {
    return <div className="text-center py-20 text-base-content/40">Failed to load hold board.</div>
  }

  const q = search.toLowerCase()
  const holdColors = ['warning', 'error', 'info', 'primary', 'success', 'secondary']

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-base-content">Hold Board</h1>
          <p className="text-sm text-base-content/60 mt-0.5">
            {data.totalHolds} jobs on hold
            {data.totalAmount > 0 && <span> &middot; {fmt(data.totalAmount)} total</span>}
          </p>
        </div>
        <div className="input input-sm max-w-xs">
          <span className="icon-[tabler--search] text-base-content/40 size-4 shrink-0" />
          <input
            type="search"
            placeholder="Search holds..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="grow"
          />
        </div>
      </div>

      {data.totalHolds === 0 ? (
        <div className="text-center py-16 text-base-content/40">
          <span className="icon-[tabler--mood-happy] size-12 block mx-auto mb-3 text-success/40" />
          <p className="text-sm">No jobs on hold. Nice!</p>
        </div>
      ) : (
        <div className="flex gap-4 overflow-x-auto pb-2 items-start">
          {data.columns.map((col, colIdx) => {
            const isUnassigned = col.key === 'unassigned'
            const color = isUnassigned ? 'neutral' : holdColors[colIdx % holdColors.length]
            const filteredJobs = q
              ? col.jobs.filter(j => j.customerName.toLowerCase().includes(q) || j.jobNumber.includes(q))
              : col.jobs

            return (
              <div key={col.key} className={'rounded-box border border-base-content/10 bg-base-100 flex flex-col min-w-[280px] w-[280px] xl:flex-1 xl:w-auto xl:min-w-0 shrink-0' + (isUnassigned ? ' border-t-[3px] border-t-base-content/20' : ' border-t-[3px] border-t-' + color)}>
                <div className="flex items-center justify-between px-4 py-3 border-b border-base-content/10">
                  <div className="flex items-center gap-2">
                    <span className="icon-[tabler--hand-stop] size-4 text-base-content/40" />
                    <h3 className="font-semibold text-sm text-base-content">{col.label}</h3>
                    <span className={'badge badge-sm badge-' + color}>{filteredJobs.length}</span>
                  </div>
                </div>

                <div className="p-2 space-y-2 max-h-[calc(100vh-14rem)] overflow-y-auto">
                  {filteredJobs.length === 0 ? (
                    <div className="text-center py-6 text-base-content/30 text-xs">
                      {q ? 'No matches' : 'No jobs'}
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
                          <span className={'text-xs ' + (job.daysSince >= 90 ? 'text-error font-medium' : job.daysSince >= 30 ? 'text-warning' : 'text-base-content/40')}>
                            {job.daysSince}d
                          </span>
                        </div>
                        {isUnassigned && data.holdReasons.length > 0 && (
                          <select
                            className="select select-xs w-full text-xs mt-2"
                            value=""
                            onChange={e => { if (e.target.value) assignReason(job.id, e.target.value) }}
                          >
                            <option value="">Assign hold reason...</option>
                            {data.holdReasons.map(r => (
                              <option key={r} value={r}>{r}</option>
                            ))}
                          </select>
                        )}
                        {!isUnassigned && (
                          <select
                            className="select select-xs w-full text-xs mt-2 opacity-50 hover:opacity-100"
                            value={job.holdReasonName || ''}
                            onChange={e => assignReason(job.id, e.target.value)}
                          >
                            <option value="">Move to Unassigned</option>
                            {data.holdReasons.map(r => (
                              <option key={r} value={r}>{r}</option>
                            ))}
                          </select>
                        )}
                      </div>
                    ))
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
