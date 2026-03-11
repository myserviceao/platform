import { useState, useEffect, useCallback, useRef } from 'react'
import { useTheme, type Theme } from '@/hooks/useTheme'
import { useAuth } from '@/hooks/useAuth'

const THEMES: { value: Theme; label: string; description: string; mode: 'dark' | 'light' }[] = [
  { value: 'dark', label: 'Dark', description: 'Default dark theme', mode: 'dark' },
  { value: 'black', label: 'Black', description: 'Pure black OLED-friendly', mode: 'dark' },
  { value: 'shadcn', label: 'Shadcn', description: 'Inspired by shadcn/ui', mode: 'dark' },
  { value: 'vscode', label: 'VS Code', description: 'Editor-inspired dark', mode: 'dark' },
  { value: 'spotify', label: 'Spotify', description: 'Music-inspired green on dark', mode: 'dark' },
  { value: 'slack', label: 'Slack', description: 'Workspace-inspired dark', mode: 'dark' },
  { value: 'valorant', label: 'Valorant', description: 'Gaming-inspired red on dark', mode: 'dark' },
  { value: 'claude', label: 'Claude', description: 'Warm, Anthropic-inspired', mode: 'dark' },
  { value: 'luxury', label: 'Luxury', description: 'Elegant with gold accents', mode: 'dark' },
  { value: 'light', label: 'Light', description: 'Clean bright theme', mode: 'light' },
  { value: 'corporate', label: 'Corporate', description: 'Professional blue tones', mode: 'light' },
  { value: 'soft', label: 'Soft', description: 'Gentle pastel colors', mode: 'light' },
  { value: 'pastel', label: 'Pastel', description: 'Soft candy colors', mode: 'light' },
  { value: 'gourmet', label: 'Gourmet', description: 'Warm cream and earthy', mode: 'light' },
  { value: 'ghibli', label: 'Ghibli', description: 'Anime-inspired soft tones', mode: 'light' },
  { value: 'mintlify', label: 'Mintlify', description: 'Modern docs-inspired', mode: 'light' },
  { value: 'perplexity', label: 'Perplexity', description: 'AI-search inspired', mode: 'light' },
]

interface Vendor { id: number; name: string; contactName?: string; phone?: string; email?: string }


function ProfileSettings() {
  const { user } = useAuth()
  const [title, setTitle] = useState(user?.title || '')
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [logoPreview, setLogoPreview] = useState(user?.tenant?.logoUrl || '')
  const [msg, setMsg] = useState('')
  const fileRef = useRef<HTMLInputElement>(null)

  const saveTitle = async () => {
    setSaving(true); setMsg('')
    const res = await fetch('/api/profile/title', {
      method: 'PUT', credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title })
    })
    if (res.ok) setMsg('Title saved! Reload to see changes in sidebar.')
    else setMsg('Failed to save title')
    setSaving(false)
  }

  const uploadLogo = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setUploading(true); setMsg('')
    const form = new FormData()
    form.append('file', file)
    const res = await fetch('/api/profile/logo', {
      method: 'POST', credentials: 'include', body: form
    })
    if (res.ok) {
      const d = await res.json()
      setLogoPreview(d.logoUrl)
      setMsg('Logo uploaded! Reload to see changes in sidebar.')
    } else {
      const d = await res.json()
      setMsg(d.error || 'Upload failed')
    }
    setUploading(false)
  }

  const removeLogo = async () => {
    const res = await fetch('/api/profile/logo', { method: 'DELETE', credentials: 'include' })
    if (res.ok) {
      setLogoPreview('')
      setMsg('Logo removed.')
    }
  }

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-5">
        <div>
          <h2 className="text-base-content font-semibold text-base">Profile & Branding</h2>
          <p className="text-base-content/60 text-sm mt-0.5">Your logo and display title shown in the sidebar.</p>
        </div>

        {msg && (
          <div className="alert alert-soft alert-info text-sm py-2">{msg}</div>
        )}

        {/* Logo upload */}
        <div className="flex items-center gap-4">
          {logoPreview ? (
            <img src={logoPreview} alt="Logo" className="max-h-16 max-w-[10rem] object-contain" />
          ) : (
            <div className="w-16 h-16 rounded-full bg-base-200 flex items-center justify-center">
              <span className="icon-[tabler--photo] size-6 text-base-content/30" />
            </div>
          )}
          <div className="space-y-1">
            <input ref={fileRef} type="file" accept="image/*" onChange={uploadLogo} className="hidden" />
            <button onClick={() => fileRef.current?.click()} className="btn btn-sm btn-primary gap-1" disabled={uploading}>
              <span className="icon-[tabler--upload] size-4" />
              {uploading ? 'Uploading...' : 'Upload Logo'}
            </button>
            {logoPreview && (
              <button onClick={removeLogo} className="btn btn-sm btn-ghost text-error gap-1">
                <span className="icon-[tabler--trash] size-3.5" /> Remove
              </button>
            )}
            <p className="text-xs text-base-content/40">PNG, JPG, or SVG. Max 2MB.</p>
          </div>
        </div>

        {/* Title */}
        <div className="space-y-2">
          <label className="text-sm font-medium text-base-content">Display Title</label>
          <div className="flex gap-2">
            <input
              className="input input-sm flex-1"
              placeholder="e.g. CEO, Operations Manager, Owner"
              value={title}
              onChange={e => setTitle(e.target.value)}
            />
            <button onClick={saveTitle} className="btn btn-sm btn-primary" disabled={saving}>
              {saving ? 'Saving...' : 'Save'}
            </button>
          </div>
          <p className="text-xs text-base-content/40">Shown under your name in the sidebar. Leave blank to show your role.</p>
        </div>
      </div>
    </div>
  )
}

export function SettingsPage() {
  const { theme, setTheme } = useTheme()
  const [vendors, setVendors] = useState<Vendor[]>([])
  const [vendorForm, setVendorForm] = useState({ name: '', contactName: '', phone: '', email: '' })
  const [showVendorForm, setShowVendorForm] = useState(false)
  const [vendorSaving, setVendorSaving] = useState(false)
  const [vendorError, setVendorError] = useState('')

  const fetchVendors = useCallback(async () => {
    const res = await fetch('/api/ap/vendors', { credentials: 'include' })
    if (res.ok) setVendors(await res.json())
  }, [])

  useEffect(() => { fetchVendors() }, [fetchVendors])

  const addVendor = async () => {
    if (!vendorForm.name.trim()) { setVendorError('Name is required'); return }
    setVendorSaving(true); setVendorError('')
    const res = await fetch('/api/ap/vendors', {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(vendorForm)
    })
    if (res.ok) {
      const v = await res.json()
      setVendors(prev => [...prev, v].sort((a, b) => a.name.localeCompare(b.name)))
      setVendorForm({ name: '', contactName: '', phone: '', email: '' })
      setShowVendorForm(false)
    } else {
      const d = await res.json(); setVendorError(d.error || 'Failed')
    }
    setVendorSaving(false)
  }

  const deleteVendor = async (id: number) => {
    if (!confirm('Delete this vendor?')) return
    const res = await fetch(`/api/ap/vendors/${id}`, { method: 'DELETE', credentials: 'include' })
    if (res.ok) setVendors(prev => prev.filter(v => v.id !== id))
    else { const d = await res.json(); alert(d.error || 'Failed to delete') }
  }

  return (
    <div className="max-w-2xl mx-auto space-y-8 py-2">
      <div>
        <h1 className="text-base-content text-2xl font-semibold">Settings</h1>
        <p className="text-base-content/60 text-sm mt-1">Manage your account preferences.</p>
      </div>

      {/* Theme */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-5">
          <div>
          <h2 className="text-base-content font-semibold text-base">Integrations</h2>
          <p className="text-base-content/40 text-sm">Manage your connected services.</p>
          <a href="/app/servicetitan" className="link link-primary text-sm w-fit">ServiceTitan settings →</a>
        </div>
      </div>
    </div>
  )
}
