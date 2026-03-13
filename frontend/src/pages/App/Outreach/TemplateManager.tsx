import { useState, useEffect } from 'react'

interface Template {
  id: number
  name: string
  type: string
  channel: string
  subject: string | null
  body: string
  isDefault: boolean
}

const TYPE_LABELS: Record<string, string> = {
  pm_reminder: 'PM Reminders',
  post_service: 'Post-Service',
  win_back: 'Win-Back',
  seasonal: 'Seasonal',
}

const MERGE_TAGS: Record<string, string[]> = {
  pm_reminder: ['customerName', 'companyName', 'phone', 'lastPmDate', 'daysOverdue'],
  post_service: ['customerName', 'companyName', 'phone', 'jobType', 'technicianName', 'completionDate'],
  win_back: ['customerName', 'companyName', 'phone', 'lastServiceDate', 'monthsSinceService', 'lifetimeSpend'],
  seasonal: ['customerName', 'companyName', 'phone'],
}

const SAMPLE_DATA: Record<string, string> = {
  customerName: 'John Smith',
  companyName: 'ABC HVAC',
  phone: '(555) 123-4567',
  lastPmDate: 'Jan 15, 2025',
  daysOverdue: '45',
  jobType: 'AC Repair',
  technicianName: 'Mike Johnson',
  completionDate: 'Mar 10, 2026',
  lastServiceDate: 'Mar 10, 2025',
  monthsSinceService: '12',
  lifetimeSpend: '$4,500',
}

export function TemplateManager() {
  const [templates, setTemplates] = useState<Template[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<Template | null>(null)
  const [creating, setCreating] = useState(false)
  const [saving, setSaving] = useState(false)

  // Form state
  const [name, setName] = useState('')
  const [type, setType] = useState('pm_reminder')
  const [channel, setChannel] = useState('email')
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')

  const fetchTemplates = async () => {
    const res = await fetch('/api/outreach/templates', { credentials: 'include' })
    if (res.ok) setTemplates(await res.json())
    setLoading(false)
  }

  useEffect(() => { fetchTemplates() }, [])

  const startCreate = () => {
    setCreating(true)
    setEditing(null)
    setName('')
    setType('pm_reminder')
    setChannel('email')
    setSubject('')
    setBody('')
  }

  const startEdit = (t: Template) => {
    setEditing(t)
    setCreating(false)
    setName(t.name)
    setType(t.type)
    setChannel(t.channel)
    setSubject(t.subject ?? '')
    setBody(t.body)
  }

  const cancel = () => { setEditing(null); setCreating(false) }

  const handleSave = async () => {
    setSaving(true)
    const payload = { name, type, channel, subject: channel === 'email' ? subject : null, body }
    if (editing) {
      await fetch(`/api/outreach/templates/${editing.id}`, {
        method: 'PUT', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })
    } else {
      await fetch('/api/outreach/templates', {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })
    }
    setSaving(false)
    cancel()
    fetchTemplates()
  }

  const handleDelete = async (id: number) => {
    await fetch(`/api/outreach/templates/${id}`, { method: 'DELETE', credentials: 'include' })
    fetchTemplates()
  }

  const insertTag = (tag: string) => {
    setBody(prev => prev + `{{${tag}}}`)
  }

  const renderPreview = (text: string) => {
    let result = text
    for (const [key, value] of Object.entries(SAMPLE_DATA))
      result = result.replace(new RegExp(`\\{\\{${key}\\}\\}`, 'g'), value)
    return result
  }

  if (loading) return <div className="flex justify-center py-16"><span className="loading loading-spinner loading-md text-primary" /></div>

  const grouped = Object.keys(TYPE_LABELS).map(t => ({
    type: t,
    label: TYPE_LABELS[t],
    templates: templates.filter(tmpl => tmpl.type === t),
  }))

  const showForm = editing || creating

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-base-content">Templates</h2>
        {!showForm && (
          <button onClick={startCreate} className="btn btn-primary btn-sm gap-1">
            <span className="icon-[tabler--plus] size-4" /> New Template
          </button>
        )}
      </div>

      {showForm && (
        <div className="rounded-box border border-base-content/10 bg-base-100 p-4 space-y-4">
          <h3 className="font-semibold text-base-content">{editing ? 'Edit Template' : 'New Template'}</h3>

          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
            <div>
              <label className="text-xs text-base-content/50 mb-1 block">Name</label>
              <input value={name} onChange={e => setName(e.target.value)} className="input input-sm input-bordered w-full" />
            </div>
            <div>
              <label className="text-xs text-base-content/50 mb-1 block">Type</label>
              <select value={type} onChange={e => setType(e.target.value)} className="select select-sm select-bordered w-full">
                {Object.entries(TYPE_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            </div>
            <div>
              <label className="text-xs text-base-content/50 mb-1 block">Channel</label>
              <select value={channel} onChange={e => setChannel(e.target.value)} className="select select-sm select-bordered w-full">
                <option value="email">Email</option>
                <option value="sms">SMS</option>
              </select>
            </div>
            {channel === 'email' && (
              <div>
                <label className="text-xs text-base-content/50 mb-1 block">Subject</label>
                <input value={subject} onChange={e => setSubject(e.target.value)} className="input input-sm input-bordered w-full" />
              </div>
            )}
          </div>

          {/* Merge tags */}
          <div>
            <label className="text-xs text-base-content/50 mb-1 block">Merge Tags (click to insert)</label>
            <div className="flex flex-wrap gap-1">
              {(MERGE_TAGS[type] || []).map(tag => (
                <button key={tag} onClick={() => insertTag(tag)} className="badge badge-soft badge-xs cursor-pointer hover:badge-primary">
                  {`{{${tag}}}`}
                </button>
              ))}
            </div>
          </div>

          <div>
            <label className="text-xs text-base-content/50 mb-1 block">Body</label>
            <textarea value={body} onChange={e => setBody(e.target.value)} className="textarea textarea-bordered w-full h-32 text-sm" />
          </div>

          {/* Preview */}
          <div>
            <label className="text-xs text-base-content/50 mb-1 block">Preview</label>
            <div className="rounded-box bg-base-200/50 p-3 text-sm text-base-content/80 whitespace-pre-wrap max-h-40 overflow-y-auto">
              {channel === 'email' && subject && <div className="font-semibold mb-2">{renderPreview(subject)}</div>}
              {renderPreview(body)}
            </div>
          </div>

          <div className="flex gap-2">
            <button onClick={handleSave} disabled={saving || !name || !body} className="btn btn-primary btn-sm gap-1">
              {saving ? <span className="loading loading-spinner loading-xs" /> : <span className="icon-[tabler--check] size-3.5" />}
              {editing ? 'Update' : 'Create'}
            </button>
            <button onClick={cancel} className="btn btn-ghost btn-sm">Cancel</button>
          </div>
        </div>
      )}

      {/* Template list */}
      {grouped.map(g => (
        <div key={g.type}>
          <h3 className="text-sm font-semibold text-base-content/70 mb-2">{g.label}</h3>
          {g.templates.length === 0 ? (
            <div className="text-xs text-base-content/40 mb-3">No templates. They will be auto-created on first sync.</div>
          ) : (
            <div className="space-y-1 mb-4">
              {g.templates.map(t => (
                <div key={t.id} className="flex items-center gap-3 p-2 rounded-box hover:bg-base-200/40 transition-colors">
                  <span className={`badge badge-soft badge-xs ${t.channel === 'email' ? 'badge-info' : 'badge-warning'}`}>{t.channel}</span>
                  <span className="text-sm text-base-content flex-1">{t.name}</span>
                  {t.isDefault && <span className="badge badge-ghost badge-xs">Default</span>}
                  <button onClick={() => startEdit(t)} className="btn btn-ghost btn-xs">
                    <span className="icon-[tabler--edit] size-3.5" />
                  </button>
                  {!t.isDefault && (
                    <button onClick={() => handleDelete(t.id)} className="btn btn-ghost btn-xs text-error/60 hover:text-error">
                      <span className="icon-[tabler--trash] size-3.5" />
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      ))}
    </div>
  )
}
