import { useState, useEffect } from 'react'
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, Line, ComposedChart, ReferenceLine } from 'recharts'

interface ForecastWeek {
  week: number
  label: string
  arCollections: number
  scheduledRevenue: number
  totalInflow: number
  apPayments: number
  netCashFlow: number
  runningBalance: number
}

interface CashFlowData {
  currentArBalance: number
  currentApBalance: number
  netPosition: number
  forecastWeeks: ForecastWeek[]
  totalProjectedInflow: number
  totalProjectedOutflow: number
  scheduledJobCount: number
  arInvoiceCount: number
  apBillCount: number
}

const fmt = (n: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(n)
const fmtK = (n: number) => n >= 1000 ? `$${(n / 1000).toFixed(1)}k` : fmt(n)

export function CashFlowPage() {
  const [data, setData] = useState<CashFlowData | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch('/api/dashboard/cashflow-forecast', { credentials: 'include' })
      .then(r => r.json())
      .then(d => { if (!d.error) setData(d) })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <span className="loading loading-spinner loading-lg text-primary" />
      </div>
    )
  }

  if (!data) {
    return <div className="text-center py-10 text-base-content/50">Unable to load forecast data</div>
  }

  const chartData = data.forecastWeeks.map(w => ({
    name: w.label,
    'AR Collections': w.arCollections,
    'Job Revenue': w.scheduledRevenue,
    'AP Payments': -w.apPayments,
    'Net Cash Flow': w.netCashFlow,
    'Running Balance': w.runningBalance,
  }))

  const netFlow = data.totalProjectedInflow - data.totalProjectedOutflow
  const healthColor = netFlow >= 0 ? 'text-success' : 'text-error'
  const healthLabel = netFlow >= 0 ? 'Positive' : 'Negative'

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-base-content">Cash Flow Forecast</h1>
          <p className="text-sm text-base-content/50">8-week projected cash inflows & outflows</p>
        </div>
        <div className={`text-right`}>
          <div className="text-xs text-base-content/40 uppercase tracking-wide">8-Week Net Flow</div>
          <div className={`text-2xl font-bold ${healthColor}`}>{fmt(netFlow)}</div>
          <div className={`text-xs ${healthColor}`}>{healthLabel} Cash Flow</div>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-4">
            <div className="text-xs text-base-content/40 uppercase">Projected Inflow</div>
            <div className="text-xl font-bold text-success">{fmt(data.totalProjectedInflow)}</div>
            <div className="text-xs text-base-content/50">{data.arInvoiceCount} invoices + {data.scheduledJobCount} jobs</div>
          </div>
        </div>
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-4">
            <div className="text-xs text-base-content/40 uppercase">Projected Outflow</div>
            <div className="text-xl font-bold text-error">{fmt(data.totalProjectedOutflow)}</div>
            <div className="text-xs text-base-content/50">{data.apBillCount} bills due</div>
          </div>
        </div>
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-4">
            <div className="text-xs text-base-content/40 uppercase">Current AR</div>
            <div className="text-xl font-bold text-info">{fmt(data.currentArBalance)}</div>
            <div className="text-xs text-base-content/50">outstanding receivables</div>
          </div>
        </div>
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-4">
            <div className="text-xs text-base-content/40 uppercase">Current AP</div>
            <div className="text-xl font-bold text-warning">{fmt(data.currentApBalance)}</div>
            <div className="text-xs text-base-content/50">unpaid payables</div>
          </div>
        </div>
      </div>

      {/* Main Chart */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body p-4">
          <h2 className="font-semibold text-sm text-base-content mb-4">Weekly Cash Flow Breakdown</h2>
          <div className="h-80">
            <ResponsiveContainer width="100%" height="100%">
              <ComposedChart data={chartData} margin={{ top: 5, right: 20, left: 10, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="currentColor" opacity={0.1} />
                <XAxis dataKey="name" tick={{ fontSize: 12 }} stroke="currentColor" opacity={0.4} />
                <YAxis tickFormatter={(v) => fmtK(Math.abs(v))} tick={{ fontSize: 11 }} stroke="currentColor" opacity={0.4} />
                <Tooltip
                  formatter={(value: number, name: string) => [fmt(Math.abs(value)), name]}
                  contentStyle={{ backgroundColor: 'oklch(var(--b1))', border: '1px solid oklch(var(--bc) / 0.1)', borderRadius: '8px', fontSize: '12px' }}
                  labelStyle={{ fontWeight: 600, marginBottom: 4 }}
                />
                <Legend wrapperStyle={{ fontSize: '11px' }} />
                <ReferenceLine y={0} stroke="currentColor" opacity={0.2} />
                <Bar dataKey="AR Collections" stackId="inflow" fill="#4ade80" radius={[0, 0, 0, 0]} />
                <Bar dataKey="Job Revenue" stackId="inflow" fill="#60a5fa" radius={[4, 4, 0, 0]} />
                <Bar dataKey="AP Payments" fill="#f87171" radius={[0, 0, 4, 4]} />
                <Line type="monotone" dataKey="Running Balance" stroke="#a78bfa" strokeWidth={2} dot={{ r: 4 }} />
              </ComposedChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      {/* Weekly Detail Table */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body p-0">
          <div className="px-4 pt-4 pb-3 border-b border-base-200">
            <h2 className="font-semibold text-sm text-base-content">Weekly Breakdown</h2>
          </div>
          <div className="overflow-x-auto">
            <table className="table table-sm">
              <thead>
                <tr className="text-xs text-base-content/40 uppercase">
                  <th>Week</th>
                  <th className="text-right">AR Collections</th>
                  <th className="text-right">Job Revenue</th>
                  <th className="text-right">Total Inflow</th>
                  <th className="text-right">AP Payments</th>
                  <th className="text-right">Net Cash Flow</th>
                  <th className="text-right">Running Balance</th>
                </tr>
              </thead>
              <tbody>
                {data.forecastWeeks.map((w) => (
                  <tr key={w.week} className="hover:bg-base-200/40">
                    <td className="text-sm font-medium">{w.label}</td>
                    <td className="text-sm text-right text-success">{fmt(w.arCollections)}</td>
                    <td className="text-sm text-right text-info">{fmt(w.scheduledRevenue)}</td>
                    <td className="text-sm text-right font-semibold text-success">{fmt(w.totalInflow)}</td>
                    <td className="text-sm text-right text-error">{fmt(w.apPayments)}</td>
                    <td className={`text-sm text-right font-semibold ${w.netCashFlow >= 0 ? 'text-success' : 'text-error'}`}>{fmt(w.netCashFlow)}</td>
                    <td className={`text-sm text-right font-bold ${w.runningBalance >= 0 ? 'text-base-content' : 'text-error'}`}>{fmt(w.runningBalance)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="border-t-2 border-base-300 font-bold">
                  <td className="text-sm">Total</td>
                  <td className="text-sm text-right text-success">{fmt(data.forecastWeeks.reduce((s, w) => s + w.arCollections, 0))}</td>
                  <td className="text-sm text-right text-info">{fmt(data.forecastWeeks.reduce((s, w) => s + w.scheduledRevenue, 0))}</td>
                  <td className="text-sm text-right text-success">{fmt(data.totalProjectedInflow)}</td>
                  <td className="text-sm text-right text-error">{fmt(data.totalProjectedOutflow)}</td>
                  <td className={`text-sm text-right ${netFlow >= 0 ? 'text-success' : 'text-error'}`}>{fmt(netFlow)}</td>
                  <td />
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      </div>
    </div>
  )
}
