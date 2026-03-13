import { useState, useEffect } from 'react'

interface OutreachItem {
  id: number
  type: string
  channel: string
  subject: string | null
  body: string
  customerName: string | null
}

interface Template {
  id: number
  name: string
  type: string
  channel: string
  subject: string | null
  body: string
}

interface Props {
  item: OutreachItem
  onClose: () => void
  onSaved: () => void
  onSent: () => void
}

export function MessageEditorModal({ item, onClose, onSaved, onSent }: Props) {
  const [subject, setSubject] = useState(item.subject ?? '')
  const [body, setBody] = useState(item.body)
  const [channel, setChannel] = useState(item.channel)
  const [templates, setTemplates] = useState<Template[]>([])
  const [saving, setSaving] = useState(false)
  const [sending, setSending] = useState(false)

  useEffect(() => {
    fetch('/api/outreach/templates', { credentials: 'include' })
      .then(r => r.ok ? r.json() : [])
      .then(setTemplates)
  }, [])

  const applyTemplate = (id: number) => {
    const t = templates.find(t => t.id === id)
    if (!t) return
    setSubject(t.subject ?? '')
    setBody(t.body)
    setChannel(t.channel)
  }

  const handleSave = async () => {
    setSaving(true)
    await fetch(`/api/outreach/${item.id}`, {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ subject: channel === 'email' ? subject : null, body, channel }),
    })
    setSaving(false)
    onSaved()
  }

  const handleSend = async () => {
    setSending(true)
    // Save first then send
    await fetch(`/api/outreach/${item.id}`, {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ subject: channel === 'email' ? subject : null, body, channel }),
    })
    await fetch(`/api/outreach/${item.id}/send`, { method: 'POST', credentials: 'include' })
    setSending(false)
    onSent()
  }

  const typeTemplates = templates.filter(t => t.type === item.type)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div className="bg-base-100 rounded-box border border-base-content/10 shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between p-4 border-b border-base-content/10">
          <h3 className="text-lg font-semibold text-base-content">Edit Message</h3>
          <button onClick={onClose} className="btn btn-ghost btn-sm btn-circle">
            <span className="icon-[tabler--x] size-4" />
          </button>
        </div>

        <div className="p-4 space-y-4">
          {/* Customer */}
          <div className="text-sm text-base-content/60">
            To: <span className="font-medium text-base-content">{item.customerName}</span>
          </div>

          {/* Template selector */}
          {typeTemplates.length > 0 && (
            <div>
              <label className="text-xs text-base-content/50 mb-1 block">Apply Template</label>
              <select onChange={e => applyTemplate(Number(e.target.value))} defaultValue="" className="select select-sm select-bordered w-full">
                <option value="" disabled>Select a template...</option>
                {typeTemplates.map(t => (
                  <option key={t.id} value={t.id}>{t.name} ({t.channel})</option>
                ))}
              </select>
            </div>
          )}

          {/* Channel toggle */}
          <div>
            <label className="text-xs text-base-content/50 mb-1 block">Channel</label>
            <div className="flex gap-2">
              <button onClick={() => setChannel('email')} className={`btn btn-sm ${channel === 'email' ? 'btn-primary' : 'btn-ghost'}`}>
                <span className="icon-[tabler--mail] size-4" /> Email
              </button>
              <button onClick={() => setChannel('sms')} className={`btn btn-sm ${channel === 'sms' ? 'btn-primary' : 'btn-ghost'}`}>
                <span className="icon-[tabler--message] size-4" /> SMS
              </button>
            </div>
          </div>

          {/* Subject (email only) */}
          {channel === 'email' && (
            <div>
              <label className="text-xs text-base-content/50 mb-1 block">Subject</label>
              <input
                value={subject}
                onChange={e => setSubject(e.target.value)}
                className="input input-sm input-bordered w-full"
                placeholder="Email subject..."
              />
            </div>
          )}

          {/* Body */}
          <div>
            <label className="text-xs text-base-content/50 mb-1 block">Message</label>
            <textarea
              value={body}
              onChange={e => setBody(e.target.value)}
              className="textarea textarea-bordered w-full h-40 text-sm"
              placeholder="Message body..."
            />
          </div>

          {/* Preview */}
          <div>
            <label className="text-xs text-base-content/50 mb-1 block">Preview</label>
            <div className="rounded-box bg-base-200/50 p-3 text-sm text-base-content/80 whitespace-pre-wrap max-h-40 overflow-y-auto">
              {channel === 'email' && subject && (
                <div className="font-semibold mb-2">{subject}</div>
              )}
              {body}
            </div>
          </div>
        </div>

        {/* Actions */}
        <div className="flex items-center justify-end gap-2 p-4 border-t border-base-content/10">
          <button onClick={onClose} className="btn btn-ghost btn-sm">Cancel</button>
          <button onClick={handleSave} disabled={saving} className="btn btn-sm gap-1">
            {saving ? <span className="loading loading-spinner loading-xs" /> : <span className="icon-[tabler--device-floppy] size-3.5" />}
            Save
          </button>
          <button onClick={handleSend} disabled={sending} className="btn btn-primary btn-sm gap-1">
            {sending ? <span className="loading loading-spinner loading-xs" /> : <span className="icon-[tabler--send] size-3.5" />}
            Send
          </button>
        </div>
      </div>
    </div>
  )
}
