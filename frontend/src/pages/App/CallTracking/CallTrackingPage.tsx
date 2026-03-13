import { useState, useEffect } from 'react'
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts'

interface AgentStats { agent: string; inbound: number; outbound: number; booked: number; total: number; bookingRate: number }
interface DayStats { date: string; inbound: number; outbound: number; booked: number }
interface CallData {
  totalCalls: number; inbound: number; outbound: number; booked: number
  abandoned: number; unbooked: number; bookingRate: number; days: number
  byAgent: AgentStats[]; byDay: DayStats[]
}

const fmtPct = (n: number) => `${n}%`

export function CallTrackingPage() {
  const [data, setData] = useState<CallData | null>(null)
  const [loading, setLoading] = useState(true)
  const [days, setDays] = useState(30)

  useEffect(() => {
    setLoading(true)
    fetch(`/api/calls/summary?days=${days}`, { credentials: 'include' })
      .then(r => r.json())
      .then(d => { if (!d.error) setData(d) })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [days])

  if (loading) return <div className="flex items-center justify-center py-20"><span className="loading loading-spinner loading-lg text-primary" /></div>
  if (!data) return <div className="text-center py-10 text-base-content/50">Unable to load call data</div>

  const chartData = data.byDay.map(d => ({
    name: new Date(d.date + 'T12:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
    Inbound: d.inbound, Outbound: d.outbound, Booked: d.booked
  }))

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-base-content">Call Tracking</h1>
          <p className="text-sm text-base-content/50">Phone call metrics from ServiceTitan</p>
        </div>
        <select value={days} onChange={e => setDays(Number(e.target.value))} className="select select-sm select-bordered">
          <option value={7}>Last 7 days</option>
          <option value={14}>Last 14 days</option>
          <option value={30}>Last 30 days</option>
          <option value={90}>Last 90 days</option>
        </select>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-6 gap-3">
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Total Calls</div>
          <div className="text-xl font-bold text-base-content">{data.totalCalls}</div>
        </div></div>
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Inbound</div>
          <div className="text-xl font-bold text-info">{data.inbound}</div>
        </div></div>
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Outbound</div>
          <div className="text-xl font-bold text-primary">{data.outbound}</div>
        </div></div>
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Booked</div>
          <div className="text-xl font-bold text-success">{data.booked}</div>
        </div></div>
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Abandoned</div>
          <div className="text-xl font-bold text-error">{data.abandoned}</div>
        </div></div>
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Booking Rate</div>
          <div className="text-xl font-bold text-success">{fmtPct(data.bookingRate)}</div>
        </div></div>
      </div>

      {/* Chart + Agent Table side by side */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
        {/* Daily Chart */}
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-4">
            <h2 className="font-semibold text-sm text-base-content mb-3">Daily Call Volume</h2>
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chartData} margin={{ top: 5, right: 10, left: 0, bottom: 5 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="currentColor" opacity={0.1} />
                  <XAxis dataKey="name" tick={{ fontSize: 10 }} stroke="currentColor" opacity={0.4} />
                  <YAxis tick={{ fontSize: 11 }} stroke="currentColor" opacity={0.4} />
                  <Tooltip contentStyle={{ backgroundColor: 'var(--fallback-b1, oklch(var(--b1)))', border: '1px solid rgba(128,128,128,0.2)', borderRadius: '8px', fontSize: '12px' }} />
                  <Legend wrapperStyle={{ fontSize: '11px' }} />
                  <Bar dataKey="Inbound" fill="#60a5fa" radius={[2, 2, 0, 0]} />
                  <Bar dataKey="Outbound" fill="#a78bfa" radius={[2, 2, 0, 0]} />
                  <Bar dataKey="Booked" fill="#4ade80" radius={[2, 2, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>
        </div>

        {/* Agent Performance */}
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-0">
            <div className="px-4 pt-4 pb-3 border-b border-base-200">
              <h2 className="font-semibold text-sm text-base-content">Performance by CSR / Agent</h2>
            </div>
            <div className="overflow-x-auto">
              <table className="table table-sm">
                <thead>
                  <tr className="text-xs text-base-content/40 uppercase">
                    <th>Agent</th>
                    <th className="text-right">Inbound</th>
                    <th className="text-right">Outbound</th>
                    <th className="text-right">Booked</th>
                    <th className="text-right">Total</th>
                    <th className="text-right">Book %</th>
                  </tr>
                </thead>
                <tbody>
                  {data.byAgent.map((a, i) => (
                    <tr key={i} className="hover:bg-base-200/40">
                      <td className="text-sm font-medium">{a.agent}</td>
                      <td className="text-sm text-right text-info">{a.inbound}</td>
                      <td className="text-sm text-right text-primary">{a.outbound}</td>
                      <td className="text-sm text-right text-success font-semibold">{a.booked}</td>
                      <td className="text-sm text-right">{a.total}</td>
                      <td className={`text-sm text-right font-bold ${a.bookingRate >= 60 ? 'text-success' : a.bookingRate >= 40 ? 'text-warning' : 'text-error'}`}>{fmtPct(a.bookingRate)}</td>
                    </tr>
                  ))}
                </tbody>
                {data.byAgent.length > 1 && (
                  <tfoot>
                    <tr className="border-t-2 border-base-300 font-bold">
                      <td className="text-sm">Total</td>
                      <td className="text-sm text-right text-info">{data.inbound}</td>
                      <td className="text-sm text-right text-primary">{data.outbound}</td>
                      <td className="text-sm text-right text-success">{data.booked}</td>
                      <td className="text-sm text-right">{data.totalCalls}</td>
                      <td className="text-sm text-right">{fmtPct(data.bookingRate)}</td>
                    </tr>
                  </tfoot>
                )}
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
