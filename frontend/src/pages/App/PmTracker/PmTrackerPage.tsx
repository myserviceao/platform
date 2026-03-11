<<<<<<< HEAD
import { useState, useEffect, useCallback } from 'react'

interface PmCustomer {
  stCustomerId: number
  customerName: string
  lastPmDate: string | null
  pmStatus: 'Overdue' | 'ComingDue' | 'Current'
  updatedAt: string
}

function daysAgo(dateStr: string | null): number | null {
  if (!dateStr) return null
  return Math.floor((Date.now() - new Date(dateStr).getTime()) / (1000 * 60 * 60 * 24))
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return 'Never'
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}

type TabKey = 'Overdue' | 'ComingDue' | 'Current' | 'All'

export function PmTrackerPage() {
  const [customers, setCustomers] = useState<PmCustomer[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [activeTab, setActiveTab] = useState<TabKey>('Overdue')
  const [search, setSearch] = useState('')

  const fetchData = useCallback(async () => {
    const res = await fetch('/api/dashboard/pm-tracker', { credentials: 'include' })
    if (res.ok) setCustomers(await res.json())
    else setError('Failed to load PM tracker data')
  }, [])

  useEffect(() => {
    fetchData().finally(() => setLoading(false))
  }, [fetchData])

  const overdue = customers.filter((c) => c.pmStatus === 'Overdue')
  const comingDue = customers.filter((c) => c.pmStatus === 'ComingDue')
  const current = customers.filter((c) => c.pmStatus === 'Current')

  const tabs: { key: TabKey; label: string; count: number }[] = [
    { key: 'Overdue', label: 'Overdue', count: overdue.length },
    { key: 'ComingDue', label: 'Coming Due', count: comingDue.length },
    { key: 'Current', label: 'Current', count: current.length },
    { key: 'All', label: 'All', count: customers.length },
  ]
=======
import { useState, useEffect, useCallback } from "react";

interface PmCustomer {
  stCustomerId: number;
  customerName: string;
  lastPmDate: string | null;
  pmStatus: "Overdue" | "ComingDue" | "Current";
  updatedAt: string;
}

function daysAgo(dateStr: string | null): number | null {
  if (!dateStr) return null;
  return Math.floor((Date.now() - new Date(dateStr).getTime()) / (1000 * 60 * 60 * 24));
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return "Never";
  return new Date(dateStr).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
}

const STATUS_CONFIG = {
  Overdue: {
    label: "Overdue",
    description: "Last PM was 6+ months ago",
    rowClass: "border-red-500/20 bg-red-500/5",
    badgeClass: "bg-red-500/15 text-red-400 border border-red-500/30",
    dotClass: "bg-red-500",
  },
  ComingDue: {
    label: "Coming Due",
    description: "Last PM was 4-6 months ago",
    rowClass: "border-yellow-500/20 bg-yellow-500/5",
    badgeClass: "bg-yellow-500/15 text-yellow-400 border border-yellow-500/30",
    dotClass: "bg-yellow-500",
  },
  Current: {
    label: "Current",
    description: "Last PM was under 4 months ago",
    rowClass: "border-gray-700/40 bg-transparent",
    badgeClass: "bg-green-500/15 text-green-400 border border-green-500/30",
    dotClass: "bg-green-500",
  },
};

type TabKey = "Overdue" | "ComingDue" | "Current" | "All";

export function PmTrackerPage() {
  const [customers, setCustomers] = useState<PmCustomer[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [activeTab, setActiveTab] = useState<TabKey>("Overdue");
  const [search, setSearch] = useState("");

  const fetchData = useCallback(async () => {
    const res = await fetch("/api/dashboard/pm-tracker", { credentials: "include" });
    if (res.ok) setCustomers(await res.json());
    else setError("Failed to load PM tracker data");
  }, []);

  useEffect(() => {
    fetchData().finally(() => setLoading(false));
  }, [fetchData]);

  const overdue = customers.filter((c) => c.pmStatus === "Overdue");
  const comingDue = customers.filter((c) => c.pmStatus === "ComingDue");
  const current = customers.filter((c) => c.pmStatus === "Current");

  const tabs: { key: TabKey; label: string; count: number; color: string }[] = [
    { key: "Overdue", label: "Overdue", count: overdue.length, color: "text-red-400" },
    { key: "ComingDue", label: "Coming Due", count: comingDue.length, color: "text-yellow-400" },
    { key: "Current", label: "Current", count: current.length, color: "text-green-400" },
    { key: "All", label: "All", count: customers.length, color: "text-gray-400" },
  ];
>>>>>>> 622fa3a658643b805f9a000b7b37bfa2a38411d5

  const tabData: Record<TabKey, PmCustomer[]> = {
    Overdue: overdue,
    ComingDue: comingDue,
    Current: current,
    All: customers,
<<<<<<< HEAD
  }

  const filtered = tabData[activeTab].filter((c) =>
    c.customerName.toLowerCase().includes(search.toLowerCase())
  )

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <span className="loading loading-spinner loading-md text-primary" />
      </div>
    )
  }

  return (
    <div className="max-w-5xl mx-auto space-y-6 py-2">
      {/* Header */}
      <div>
        <h1 className="text-base-content text-2xl font-semibold">PM Tracker</h1>
        <p className="text-base-content/60 text-sm mt-1">
          Preventive maintenance status for all customers based on completed PM jobs in ServiceTitan.
=======
  };

  const filtered = tabData[activeTab].filter((c) =>
    c.customerName.toLowerCase().includes(search.toLowerCase())
  );

  if (loading) {
    return (
      <div className="p-8 flex items-center justify-center">
        <div className="w-6 h-6 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div className="p-6 max-w-5xl mx-auto space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-white">PM Tracker</h1>
        <p className="text-sm text-gray-400 mt-1">
          Track preventive maintenance status for all customers based on completed PM jobs in ServiceTitan.
>>>>>>> 622fa3a658643b805f9a000b7b37bfa2a38411d5
        </p>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-3 gap-4">
<<<<<<< HEAD
        {(
          [
            {
              key: 'Overdue',
              label: 'Overdue',
              count: overdue.length,
              desc: 'Last PM 6+ months ago',
              card: 'border-error/30 bg-error/5',
              icon: 'icon-[tabler--alert-circle]',
              iconColor: 'text-error',
            },
            {
              key: 'ComingDue',
              label: 'Coming Due',
              count: comingDue.length,
              desc: 'Last PM 4-6 months ago',
              card: 'border-warning/30 bg-warning/5',
              icon: 'icon-[tabler--clock]',
              iconColor: 'text-warning',
            },
            {
              key: 'Current',
              label: 'Current',
              count: current.length,
              desc: 'Last PM under 4 months',
              card: 'border-success/30 bg-success/5',
              icon: 'icon-[tabler--circle-check]',
              iconColor: 'text-success',
            },
          ] as const
        ).map((s) => (
          <button
            key={s.key}
            onClick={() => setActiveTab(s.key as TabKey)}
            className={`rounded-box border-2 p-4 text-left transition-all ${
              activeTab === s.key
                ? s.card + ' border-2'
                : 'border-base-content/10 bg-base-100 hover:border-base-content/20'
            }`}
          >
            <div className="flex items-center gap-2 mb-1">
              <span className={`${s.icon} size-4 ${s.iconColor}`} />
              <span className="text-xs text-base-content/60 font-medium">{s.label}</span>
            </div>
            <div className="text-2xl font-bold text-base-content">{s.count}</div>
            <div className="text-xs text-base-content/40 mt-0.5">{s.desc}</div>
          </button>
        ))}
=======
        {(["Overdue", "ComingDue", "Current"] as const).map((status) => {
          const cfg = STATUS_CONFIG[status];
          const count = tabData[status].length;
          return (
            <button
              key={status}
              onClick={() => setActiveTab(status)}
              className={`rounded-xl border p-4 text-left transition-all ${
                activeTab === status ? cfg.rowClass + " ring-1 ring-inset " + (status === "Overdue" ? "ring-red-500/40" : status === "ComingDue" ? "ring-yellow-500/40" : "ring-green-500/40") : "border-gray-700 bg-gray-800/40 hover:border-gray-600"
              }`}
            >
              <div className="flex items-center gap-2 mb-1">
                <span className={`w-2 h-2 rounded-full ${cfg.dotClass}`} />
                <span className="text-xs text-gray-400 font-medium">{cfg.label}</span>
              </div>
              <div className={`text-2xl font-bold ${status === "Overdue" ? "text-red-400" : status === "ComingDue" ? "text-yellow-400" : "text-green-400"}`}>
                {count}
              </div>
              <div className="text-xs text-gray-500 mt-0.5">{cfg.description}</div>
            </button>
          );
        })}
>>>>>>> 622fa3a658643b805f9a000b7b37bfa2a38411d5
      </div>

      {/* Tabs + search */}
      <div className="flex items-center justify-between gap-4">
<<<<<<< HEAD
        <div className="tabs tabs-bordered flex-1">
=======
        <div className="flex gap-1 border-b border-gray-700 flex-1">
>>>>>>> 622fa3a658643b805f9a000b7b37bfa2a38411d5
          {tabs.map((tab) => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
<<<<<<< HEAD
              className={`tab ${activeTab === tab.key ? 'tab-active' : ''}`}
            >
              {tab.label}
              <span className="badge badge-soft badge-sm ms-2">{tab.count}</span>
            </button>
          ))}
        </div>
        <div className="input input-sm max-w-52">
          <span className="icon-[tabler--search] text-base-content/40 size-4 shrink-0" />
          <input
            type="search"
            placeholder="Search customers..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="grow"
          />
        </div>
      </div>

      {/* Content */}
      {error ? (
        <div className="alert alert-soft alert-error text-sm">
          <span className="icon-[tabler--alert-circle] size-4 shrink-0" />
          {error}
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-16 text-base-content/40 text-sm">
          {search ? 'No customers match your search.' : 'No customers in this category.'}
        </div>
      ) : (
        <div className="rounded-box border border-base-content/10 overflow-hidden">
          <table className="table table-sm">
            <thead>
              <tr>
                <th>Customer</th>
                <th>Last PM</th>
                <th>Days Since</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((c) => {
                const days = daysAgo(c.lastPmDate)
                const badgeClass =
                  c.pmStatus === 'Overdue'
                    ? 'badge-error'
                    : c.pmStatus === 'ComingDue'
                    ? 'badge-warning'
                    : 'badge-success'
                return (
                  <tr key={c.stCustomerId} className="hover">
                    <td className="font-medium text-base-content">{c.customerName}</td>
                    <td className="text-base-content/70">{formatDate(c.lastPmDate)}</td>
                    <td className="text-base-content/60">
                      {days !== null ? `${days} days` : '—'}
                    </td>
                    <td>
                      <span className={`badge badge-soft badge-sm ${badgeClass}`}>
                        {c.pmStatus === 'ComingDue' ? 'Coming Due' : c.pmStatus}
                      </span>
                    </td>
                  </tr>
                )
=======
              className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
                activeTab === tab.key
                  ? "border-blue-500 text-white"
                  : "border-transparent text-gray-400 hover:text-gray-200"
              }`}
            >
              {tab.label}
              <span className={`ml-2 text-xs ${tab.color}`}>({tab.count})</span>
            </button>
          ))}
        </div>
        <input
          type="text"
          placeholder="Search customers..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="px-3 py-1.5 text-sm rounded-lg bg-gray-800 border border-gray-700 text-gray-200 placeholder-gray-500 focus:outline-none focus:border-gray-500 w-52"
        />
      </div>

      {/* Table */}
      {error ? (
        <div className="text-red-400 text-sm">{error}</div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-12 text-gray-500 text-sm">
          {search ? "No customers match your search." : "No customers in this category."}
        </div>
      ) : (
        <div className="rounded-xl border border-gray-700 overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-800/80 border-b border-gray-700">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">Customer</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">Last PM</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">Days Since</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-700/50">
              {filtered.map((c) => {
                const cfg = STATUS_CONFIG[c.pmStatus];
                const days = daysAgo(c.lastPmDate);
                return (
                  <tr key={c.stCustomerId} className={"transition-colors hover:bg-gray-800/40 " + cfg.rowClass}>
                    <td className="px-4 py-3 font-medium text-white">{c.customerName}</td>
                    <td className="px-4 py-3 text-gray-300">{formatDate(c.lastPmDate)}</td>
                    <td className="px-4 py-3 text-gray-400">
                      {days !== null ? `${days} days` : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${cfg.badgeClass}`}>
                        <span className={`w-1.5 h-1.5 rounded-full ${cfg.dotClass}`} />
                        {cfg.label}
                      </span>
                    </td>
                  </tr>
                );
>>>>>>> 622fa3a658643b805f9a000b7b37bfa2a38411d5
              })}
            </tbody>
          </table>
        </div>
      )}

      {customers.length > 0 && (
<<<<<<< HEAD
        <p className="text-xs text-base-content/40 text-right">
=======
        <p className="text-xs text-gray-500 text-right">
>>>>>>> 622fa3a658643b805f9a000b7b37bfa2a38411d5
          Data from last sync. Run Sync from the Dashboard to refresh.
        </p>
      )}

      {customers.length === 0 && !loading && (
<<<<<<< HEAD
        <div className="text-center py-16 text-base-content/40 space-y-2">
          <p className="text-lg font-medium">No PM data yet</p>
          <p className="text-sm">
            Run a sync from the Dashboard to populate PM customer history.
          </p>
        </div>
      )}
    </div>
  )
=======
        <div className="text-center py-16 text-gray-500 space-y-2">
          <p className="text-lg">No PM data yet</p>
          <p className="text-sm">Run a sync from the Dashboard to populate PM customer history.</p>
        </div>
      )}
    </div>
  );
>>>>>>> 622fa3a658643b805f9a000b7b37bfa2a38411d5
}
