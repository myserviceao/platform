import { useState, useEffect } from 'react'

interface Settings {
  winBackThresholdMonths: number
  postServiceDelayHours: number
  resendApiKey: string | null
  resendFromEmail: string | null
  twilioAccountSid: string | null
  twilioAuthToken: string | null
  twilioFromPhone: string | null
  emailConfigured: boolean
  smsConfigured: boolean
}

export function OutreachSettings() {
  const [settings, setSettings] = useState<Settings | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  // Form
  const [winBackMonths, setWinBackMonths] = useState(12)
  const [postServiceHours, setPostServiceHours] = useState(48)
  const [resendApiKey, setResendApiKey] = useState('')
  const [resendFromEmail, setResendFromEmail] = useState('')
  const [twilioSid, setTwilioSid] = useState('')
  const [twilioToken, setTwilioToken] = useState('')
  const [twilioPhone, setTwilioPhone] = useState('')

  useEffect(() => {
    fetch('/api/outreach/settings', { credentials: 'include' })
      .then(r => r.ok ? r.json() : null)
      .then(data => {
        if (data) {
          setSettings(data)
          setWinBackMonths(data.winBackThresholdMonths)
          setPostServiceHours(data.postServiceDelayHours)
          setResendApiKey(data.resendApiKey ?? '')
          setResendFromEmail(data.resendFromEmail ?? '')
          setTwilioSid(data.twilioAccountSid ?? '')
          setTwilioToken(data.twilioAuthToken ?? '')
          setTwilioPhone(data.twilioFromPhone ?? '')
        }
        setLoading(false)
      })
  }, [])

  const handleSave = async () => {
    setSaving(true)
    setSaved(false)
    await fetch('/api/outreach/settings', {
      method: 'PUT', credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        winBackThresholdMonths: winBackMonths,
        postServiceDelayHours: postServiceHours,
        resendApiKey: resendApiKey || null,
        resendFromEmail: resendFromEmail || null,
        twilioAccountSid: twilioSid || null,
        twilioAuthToken: twilioToken || null,
        twilioFromPhone: twilioPhone || null,
      }),
    })
    setSaving(false)
    setSaved(true)
    setTimeout(() => setSaved(false), 2000)
  }

  if (loading) return <div className="flex justify-center py-16"><span className="loading loading-spinner loading-md text-primary" /></div>

  return (
    <div className="space-y-6 max-w-xl">
      <h2 className="text-lg font-semibold text-base-content">Outreach Settings</h2>

      {/* Thresholds */}
      <div className="rounded-box border border-base-content/10 bg-base-100 p-4 space-y-3">
        <h3 className="text-sm font-semibold text-base-content">Generation Thresholds</h3>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="text-xs text-base-content/50 mb-1 block">Win-Back Threshold (months)</label>
            <input type="number" value={winBackMonths} onChange={e => setWinBackMonths(Number(e.target.value))} className="input input-sm input-bordered w-full" min={1} />
          </div>
          <div>
            <label className="text-xs text-base-content/50 mb-1 block">Post-Service Delay (hours)</label>
            <input type="number" value={postServiceHours} onChange={e => setPostServiceHours(Number(e.target.value))} className="input input-sm input-bordered w-full" min={1} />
          </div>
        </div>
      </div>

      {/* Email (Resend) */}
      <div className="rounded-box border border-base-content/10 bg-base-100 p-4 space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-base-content">Email (Resend)</h3>
          <span className={`badge badge-soft badge-xs ${settings?.emailConfigured ? 'badge-success' : 'badge-ghost'}`}>
            {settings?.emailConfigured ? 'Configured' : 'Not Configured'}
          </span>
        </div>
        <div>
          <label className="text-xs text-base-content/50 mb-1 block">API Key</label>
          <input type="password" value={resendApiKey} onChange={e => setResendApiKey(e.target.value)} className="input input-sm input-bordered w-full" placeholder="re_..." />
        </div>
        <div>
          <label className="text-xs text-base-content/50 mb-1 block">From Email</label>
          <input type="email" value={resendFromEmail} onChange={e => setResendFromEmail(e.target.value)} className="input input-sm input-bordered w-full" placeholder="service@yourcompany.com" />
        </div>
      </div>

      {/* SMS (Twilio) */}
      <div className="rounded-box border border-base-content/10 bg-base-100 p-4 space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-base-content">SMS (Twilio)</h3>
          <span className={`badge badge-soft badge-xs ${settings?.smsConfigured ? 'badge-success' : 'badge-ghost'}`}>
            {settings?.smsConfigured ? 'Configured' : 'Not Configured'}
          </span>
        </div>
        <div>
          <label className="text-xs text-base-content/50 mb-1 block">Account SID</label>
          <input type="password" value={twilioSid} onChange={e => setTwilioSid(e.target.value)} className="input input-sm input-bordered w-full" placeholder="AC..." />
        </div>
        <div>
          <label className="text-xs text-base-content/50 mb-1 block">Auth Token</label>
          <input type="password" value={twilioToken} onChange={e => setTwilioToken(e.target.value)} className="input input-sm input-bordered w-full" />
        </div>
        <div>
          <label className="text-xs text-base-content/50 mb-1 block">From Phone Number</label>
          <input type="tel" value={twilioPhone} onChange={e => setTwilioPhone(e.target.value)} className="input input-sm input-bordered w-full" placeholder="+15551234567" />
        </div>
      </div>

      {/* Save */}
      <div className="flex items-center gap-3">
        <button onClick={handleSave} disabled={saving} className="btn btn-primary btn-sm gap-1">
          {saving ? <span className="loading loading-spinner loading-xs" /> : <span className="icon-[tabler--check] size-3.5" />}
          Save Settings
        </button>
        {saved && <span className="text-sm text-success">Saved!</span>}
      </div>
    </div>
  )
}
