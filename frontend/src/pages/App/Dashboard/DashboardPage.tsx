import { useState, useEffect, useCallback } from "react";
import { useAuth } from "@/hooks/useAuth";

interface DashboardSnapshot {
  synced: boolean;
  revenueThisMonth: number;
  revenueLastMonth: number;
  accountsReceivable: number;
  unpaidInvoiceCount: number;
  openJobCount: number;
  overduePmCount: number;
  snapshotTakenAt: string | null;
}

function fmt(n: number) {
  return new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 }).format(n);
}

function revChange(current: number, previous: number) {
  if (previous === 0) return null;
  const pct = ((current - previous) / previous) * 100;
  return { pct: Math.abs(pct).toFixed(1), up: pct >= 0 };
}

interface KpiCardProps {
  title: string;
  value: string;
  sub?: string;
  badge?: { label: string; up: boolean } | null;
  alert?: boolean;
}

function KpiCard({ title, value, sub, badge, alert }: KpiCardProps) {
  return (
    <div className={`rounded-xl border p-6 flex flex-col gap-2 ${alert ? "border-red-500/40 bg-red-500/5" : "border-gray-700 bg-gray-800/60"}`}>
      <span className="text-xs font-medium text-gray-400 uppercase tracking-wider">{title}</span>
      <span className={`text-3xl font-bold ${alert ? "text-red-400" : "text-white"}`}>{value}</span>
      {badge && (
        <span className={`text-xs font-medium ${badge.up ? "text-green-400" : "text-red-400"}`}>
          {badge.up ? "▲" : "▼"} {badge.pct}% vs last month
        </span>
      )}
      {sub && <span className="text-xs text-gray-500">{sub}</span>}
    </div>
  );
}

export function DashboardPage() {
  const { user } = useAuth();
  const [data, setData] = useState<DashboardSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [syncing, setSyncing] = useState(false);
  const [error, setError] = useState("");

  const fetchSnapshot = useCallback(async () => {
    const res = await fetch("/api/dashboard/snapshot", { credentials: "include" });
    if (res.ok) setData(await res.json());
  }, []);

  useEffect(() => {
    fetchSnapshot().finally(() => setLoading(false));
  }, [fetchSnapshot]);

  const handleSync = async () => {
    setSyncing(true);
    setError("");
    try {
      const res = await fetch("/api/dashboard/sync", { method: "POST", credentials: "include" });
      const json = await res.json();
      if (!res.ok) setError(json.error || "Sync failed");
      else setData(json);
    } catch {
      setError("Network error — please try again");
    } finally {
      setSyncing(false);
    }
  };

  if (loading) {
    return (
      <div className="p-8 flex items-center justify-center">
        <div className="w-6 h-6 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  const change = data ? revChange(data.revenueThisMonth, data.revenueLastMonth) : null;

  return (
    <div className="p-6 max-w-6xl mx-auto space-y-6">

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white">Dashboard</h1>
          {data?.snapshotTakenAt && (
            <p className="text-xs text-gray-500 mt-1">
              Last synced {new Date(data.snapshotTakenAt).toLocaleString()}
            </p>
          )}
        </div>
        <button
          onClick={handleSync}
          disabled={syncing}
          className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white rounded-lg text-sm font-medium transition-colors"
        >
          <svg className={`w-4 h-4 ${syncing ? "animate-spin" : ""}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          {syncing ? "Syncing..." : "Sync Now"}
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-3 text-sm text-red-400">
          {error}
        </div>
      )}

      {/* Not connected banner */}
      {!data?.synced && (
        <div className="bg-blue-500/10 border border-blue-500/20 rounded-xl p-5 flex items-center justify-between">
          <div>
            <p className="text-blue-300 font-medium">Connect ServiceTitan to see live data</p>
            <p className="text-blue-400/70 text-sm mt-1">Your dashboard will populate automatically after connecting.</p>
          </div>
          <a
            href="/app/servicetitan"
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-medium transition-colors whitespace-nowrap"
          >
            Connect →
          </a>
        </div>
      )}

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
        <KpiCard
          title="Revenue This Month"
          value={fmt(data?.revenueThisMonth ?? 0)}
          badge={change ? { label: `${change.pct}%`, up: change.up } : null}
          sub={`Last month: ${fmt(data?.revenueLastMonth ?? 0)}`}
        />
        <KpiCard
          title="Accounts Receivable"
          value={fmt(data?.accountsReceivable ?? 0)}
          sub={`${data?.unpaidInvoiceCount ?? 0} unpaid invoice${(data?.unpaidInvoiceCount ?? 0) !== 1 ? "s" : ""}`}
          alert={(data?.accountsReceivable ?? 0) > 0}
        />
        <KpiCard
          title="Open Work Orders"
          value={(data?.openJobCount ?? 0).toString()}
          sub="Jobs not yet completed or canceled"
        />
        <KpiCard
          title="Overdue PMs"
          value={(data?.overduePmCount ?? 0).toString()}
          sub="Recurring services past due date"
          alert={(data?.overduePmCount ?? 0) > 0}
        />
      </div>
    </div>
  );
}