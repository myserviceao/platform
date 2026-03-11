import { useState, useEffect, useCallback } from 'react'

interface ClusterCustomer {
  stCustomerId: number
  customerName: string
  locationName: string
  address: string
  lat: number
  lng: number
  pmStatus: string
  lastPmDate: string | null
  daysSince: number
}

interface Cluster {
  id: number
  count: number
  customers: ClusterCustomer[]
  centerLat: number
  centerLng: number
}

interface PlannerData {
  totalCustomers: number
  totalClusters: number
  notGeocoded: number
  radiusMinutes: number
  radiusMiles: number
  clusters: Cluster[]
}

export function PmPlannerPage() {
  const [data, setData] = useState<PlannerData | null>(null)
  const [loading, setLoading] = useState(true)
  const [geocoding, setGeocoding] = useState(false)
  const [geocodeMsg, setGeocodeMsg] = useState('')
  const [radius, setRadius] = useState(10)
  const [expandedCluster, setExpandedCluster] = useState<number | null>(null)

  const fetchData = useCallback(async () => {
    setLoading(true)
    const res = await fetch(`/api/pm-planner?radiusMinutes=${radius}`, { credentials: 'include' })
    if (res.ok) setData(await res.json())
    setLoading(false)
  }, [radius])

  useEffect(() => { fetchData() }, [fetchData])

  const handleGeocode = async () => {
    setGeocoding(true)
    setGeocodeMsg('')
    const res = await fetch('/api/pm-planner/geocode', { method: 'POST', credentials: 'include' })
    if (res.ok) {
      const d = await res.json()
      setGeocodeMsg(`Geocoded ${d.geocoded} locations. ${d.remaining} remaining.`)
      if (d.remaining > 0) {
        setGeocodeMsg(prev => prev + ' Run again to continue.')
      }
      fetchData()
    } else {
      setGeocodeMsg('Geocoding failed')
    }
    setGeocoding(false)
  }

  if (loading && !data) {
    return <div className="flex items-center justify-center py-20"><span className="loading loading-spinner loading-md text-primary" /></div>
  }

  return (
    <div className="max-w-6xl mx-auto space-y-6 py-2">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-base-content">PM Planner</h1>
          <p className="text-sm text-base-content/60 mt-1">
            Group customers by proximity to plan efficient PM routes.
          </p>
        </div>
        <div className="flex gap-2">
          <button onClick={handleGeocode} disabled={geocoding} className="btn btn-ghost btn-sm gap-1">
            <span className="icon-[tabler--map-pin] size-4" />
            {geocoding ? 'Geocoding...' : 'Geocode Addresses'}
          </button>
        </div>
      </div>

      {geocodeMsg && (
        <div className="alert alert-soft alert-info text-sm">{geocodeMsg}</div>
      )}

      {/* Stats + radius control */}
      <div className="flex items-center gap-4">
        <div className="grid grid-cols-3 gap-3 flex-1">
          <div className="rounded-box border border-base-content/10 bg-base-100 p-3 text-center">
            <div className="text-xl font-bold text-base-content">{data?.totalCustomers ?? 0}</div>
            <div className="text-xs text-base-content/50">Customers to schedule</div>
          </div>
          <div className="rounded-box border border-base-content/10 bg-base-100 p-3 text-center">
            <div className="text-xl font-bold text-primary">{data?.clusters?.filter(c => c.count > 1).length ?? 0}</div>
            <div className="text-xs text-base-content/50">Groups of 2+</div>
          </div>
          <div className="rounded-box border border-base-content/10 bg-base-100 p-3 text-center">
            <div className="text-xl font-bold text-warning">{data?.notGeocoded ?? 0}</div>
            <div className="text-xs text-base-content/50">Not geocoded yet</div>
          </div>
        </div>
        <div className="rounded-box border border-base-content/10 bg-base-100 p-3">
          <div className="text-xs text-base-content/50 mb-1">Drive radius</div>
          <div className="flex gap-1">
            {[10, 30, 60].map(r => (
              <button
                key={r}
                onClick={() => setRadius(r)}
                className={`btn btn-xs ${radius === r ? 'btn-primary' : 'btn-ghost'}`}
              >
                {r} min
              </button>
            ))}
          </div>
        </div>
      </div>

      {data?.notGeocoded && data.notGeocoded > 0 && (
        <div className="alert alert-soft alert-warning text-sm">
          <span className="icon-[tabler--alert-triangle] size-4 shrink-0" />
          {data.notGeocoded} customers don't have geocoded addresses yet. Click "Geocode Addresses" to locate them on the map. This may take a moment.
        </div>
      )}

      {/* Clusters */}
      {!data?.clusters?.length ? (
        <div className="text-center py-16 text-base-content/40">
          <span className="icon-[tabler--map-pin-off] size-12 block mx-auto mb-3 text-base-content/20" />
          <p className="text-sm">No geocoded PM customers found.</p>
          <p className="text-xs mt-1">Run a Sync from the Dashboard, then click "Geocode Addresses" above.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {/* Multi-customer clusters first */}
          {data.clusters.filter(c => c.count > 1).length > 0 && (
            <div className="space-y-2">
              <h3 className="text-sm font-semibold text-base-content/70 flex items-center gap-2">
                <span className="icon-[tabler--users-group] size-4 text-primary" />
                Grouped Customers (within {data.radiusMinutes} min / {data.radiusMiles} miles)
              </h3>
              {data.clusters.filter(c => c.count > 1).map(cluster => (
                <div key={cluster.id} className="rounded-box border border-primary/20 bg-base-100 overflow-hidden">
                  <button
                    onClick={() => setExpandedCluster(expandedCluster === cluster.id ? null : cluster.id)}
                    className="w-full flex items-center gap-3 px-4 py-3 text-left hover:bg-base-200/40 transition-colors"
                  >
                    <span className="badge badge-primary badge-sm font-bold">{cluster.count}</span>
                    <span className="text-sm font-medium text-base-content flex-1">
                      {cluster.customers.slice(0, 3).map(c => c.customerName).join(', ')}
                      {cluster.count > 3 && ` +${cluster.count - 3} more`}
                    </span>
                    <span className="text-xs text-base-content/40">
                      {cluster.customers[0]?.address?.split(',').slice(1, 3).join(',').trim()}
                    </span>
                    <span className={`icon-[tabler--chevron-down] size-4 text-base-content/30 transition-transform duration-200 ${expandedCluster === cluster.id ? 'rotate-180' : ''}`} />
                  </button>

                  {expandedCluster === cluster.id && (
                    <div className="border-t border-base-content/10">
                      <table className="table table-sm">
                        <thead>
                          <tr className="text-xs text-base-content/40 uppercase">
                            <th>Customer</th>
                            <th>Address</th>
                            <th>Status</th>
                            <th>Days Since PM</th>
                          </tr>
                        </thead>
                        <tbody>
                          {cluster.customers.map((c, i) => (
                            <tr key={i} className="row-hover">
                              <td className="font-medium text-base-content text-sm">{c.customerName}</td>
                              <td className="text-xs text-base-content/60">{c.address}</td>
                              <td>
                                <span className={`badge badge-soft badge-xs ${c.pmStatus === 'Overdue' ? 'badge-error' : 'badge-warning'}`}>
                                  {c.pmStatus === 'ComingDue' ? 'Coming Due' : c.pmStatus}
                                </span>
                              </td>
                              <td className="text-sm text-base-content/60">{c.daysSince}d</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {/* Single customers */}
          {data.clusters.filter(c => c.count === 1).length > 0 && (
            <div className="space-y-2 mt-6">
              <h3 className="text-sm font-semibold text-base-content/70 flex items-center gap-2">
                <span className="icon-[tabler--user] size-4 text-base-content/40" />
                Standalone Customers (no nearby neighbors)
              </h3>
              <div className="rounded-box border border-base-content/10 overflow-hidden">
                <table className="table table-sm">
                  <thead>
                    <tr className="text-xs text-base-content/40 uppercase">
                      <th>Customer</th>
                      <th>Address</th>
                      <th>Status</th>
                      <th>Days Since PM</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.clusters.filter(c => c.count === 1).map(cluster => {
                      const c = cluster.customers[0]
                      return (
                        <tr key={cluster.id} className="row-hover">
                          <td className="font-medium text-base-content text-sm">{c.customerName}</td>
                          <td className="text-xs text-base-content/60">{c.address}</td>
                          <td>
                            <span className={`badge badge-soft badge-xs ${c.pmStatus === 'Overdue' ? 'badge-error' : 'badge-warning'}`}>
                              {c.pmStatus === 'ComingDue' ? 'Coming Due' : c.pmStatus}
                            </span>
                          </td>
                          <td className="text-sm text-base-content/60">{c.daysSince}d</td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
