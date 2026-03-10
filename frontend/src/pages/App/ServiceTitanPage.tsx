import { useState, useEffect } from "react";

interface STStatus {
  connected: boolean;
}

interface SyncResult {
  success: boolean;
  jobsSynced: number;
  syncedAt: string;
  error?: string;
}

export default function ServiceTitanPage() {
  const [status, setStatus] = useState<STStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [lastSync, setLastSync] = useState<SyncResult | null>(null);
  const [error, setError] = useState("");

  const [form, setForm] = useState({
    clientId: "",
    clientSecret: "",
    stTenantId: "",
  });

  useEffect(() => {
    fetch("/api/servicetitan/status", { credentials: "include" })
      .then((r) => r.json())
      .then((d) => setStatus(d))
      .finally(() => setLoading(false));
  }, []);

  const handleConnect = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError("");

    try {
      const res = await fetch("/api/servicetitan/connect", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(form),
      });
      const data = await res.json();

      if (!res.ok) {
        setError(data.error || "Connection failed");
      } else {
        setStatus({ connected: true });
        setLastSync(data.sync);
        setForm({ clientId: "", clientSecret: "", stTenantId: "" });
      }
    } catch {
      setError("Network error — please try again");
    } finally {
      setSaving(false);
    }
  };

  const handleSync = async () => {
    setSyncing(true);
    setError("");
    try {
      const res = await fetch("/api/servicetitan/sync", {
        method: "POST",
        credentials: "include",
      });
      const data = await res.json();
      if (!res.ok) setError(data.error || "Sync failed");
      else setLastSync(data);
    } catch {
      setError("Network error — please try again");
    } finally {
      setSyncing(false);
    }
  };

  const handleDisconnect = async () => {
    if (!confirm("Disconnect ServiceTitan? Your synced data will remain but won't update.")) return;
    await fetch("/api/servicetitan/disconnect", { method: "POST", credentials: "include" });
    setStatus({ connected: false });
    setLastSync(null);
  };

  if (loading) return <div className="p-8 text-gray-400">Loading...</div>;

  return (
    <div className="max-w-xl mx-auto p-8">
      <h1 className="text-2xl font-bold text-white mb-2">ServiceTitan</h1>
      <p className="text-gray-400 mb-8">
        Connect your ServiceTitan account to sync jobs, invoices, and customer data.
      </p>

      {/* Connected state */}
      {status?.connected ? (
        <div className="space-y-6">
          <div className="flex items-center gap-3 bg-green-500/10 border border-green-500/30 rounded-lg p-4">
            <div className="w-3 h-3 rounded-full bg-green-500" />
            <span className="text-green-400 font-medium">Connected to ServiceTitan</span>
          </div>

          {lastSync && (
            <div className="bg-gray-800 rounded-lg p-4 text-sm text-gray-300">
              <div>Last sync: {new Date(lastSync.syncedAt).toLocaleString()}</div>
              <div>Jobs synced: {lastSync.jobsSynced}</div>
            </div>
          )}

          <div className="flex gap-3">
            <button
              onClick={handleSync}
              disabled={syncing}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white rounded-lg text-sm font-medium transition-colors"
            >
              {syncing ? "Syncing..." : "Sync Now"}
            </button>
            <button
              onClick={handleDisconnect}
              className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-gray-300 rounded-lg text-sm font-medium transition-colors"
            >
              Disconnect
            </button>
          </div>
        </div>
      ) : (
        /* Connect form */
        <form onSubmit={handleConnect} className="space-y-5">
          <div className="bg-blue-500/10 border border-blue-500/20 rounded-lg p-4 text-sm text-blue-300">
            You'll find these credentials in your{" "}
            <a
              href="https://developer.servicetitan.io"
              target="_blank"
              rel="noopener noreferrer"
              className="underline"
            >
              ServiceTitan Developer Portal
            </a>{" "}
            under your connected app.
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1">Client ID</label>
            <input
              type="text"
              required
              value={form.clientId}
              onChange={(e) => setForm({ ...form, clientId: e.target.value })}
              className="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-blue-500"
              placeholder="app-XXXXXXXXXXXXXXXX"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1">Client Secret</label>
            <input
              type="password"
              required
              value={form.clientSecret}
              onChange={(e) => setForm({ ...form, clientSecret: e.target.value })}
              className="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-blue-500"
              placeholder="••••••••••••••••"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-300 mb-1">
              ServiceTitan Tenant ID
            </label>
            <input
              type="text"
              required
              value={form.stTenantId}
              onChange={(e) => setForm({ ...form, stTenantId: e.target.value })}
              className="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-blue-500"
              placeholder="1234567"
            />
            <p className="text-xs text-gray-500 mt-1">
              Found in ServiceTitan under Settings → Integrations
            </p>
          </div>

          {error && (
            <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-3 text-sm text-red-400">
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={saving}
            className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white rounded-lg font-medium text-sm transition-colors"
          >
            {saving ? "Connecting & syncing..." : "Connect ServiceTitan"}
          </button>
        </form>
      )}
    </div>
  );
}