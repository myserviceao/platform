import { useState, useEffect } from 'react'

interface Template {
  id: number
  name: string
  type: string
  channel: string
}

interface Props {
  onClose: () => void
  onCreated: () => void
}

export function CampaignModal({ onClose, onCreated }: Props) {
  const [templates, setTemplates] = useState<Template[]>([])
  const [templateId, setTemplateId] = useState<number | null>(null)
  const [segment, setSegment] = useState('all')
  const [months, setMonths] = useState(6)
  const [generating, setGenerating] = useState(false)
  const [result, setResult] = useState<number | null>(null)

  useEffect(() => {
    fetch('/api/outreach/templates', { credentials: 'include' })
      .then(r => r.ok ? r.json() : [])
      .then((all: Template[]) => {
        const seasonal = all.filter(t => t.type === 'seasonal')
        setTemplates(seasonal)
        if (seasonal.length > 0) setTemplateId(seasonal[0].id)
      })
  }, [])

  const handleGenerate = async () => {
    if (!templateId) return
    setGenerating(true)
    const res = await fetch('/api/outreach/campaigns', {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        templateId,
        segment,
        monthsThreshold: segment === 'no_recent_service' ? months : null,
      }),
    })
    if (res.ok) {
      const data = await res.json()
      setResult(data.generated)
    }
    setGenerating(false)
  }

  const segments = [
    { value: 'all', label: 'All customers' },
    { value: 'with_pm', label: 'Customers with PM history' },
    { value: 'without_pm', label: 'Customers without PM history' },
    { value: 'no_recent_service', label: 'No service in last N months' },
  ]

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div className="bg-base-100 rounded-box border border-base-content/10 shadow-xl w-full max-w-md" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between p-4 border-b border-base-content/10">
          <h3 className="text-lg font-semibold text-base-content">New Seasonal Campaign</h3>
          <button onClick={onClose} className="btn btn-ghost btn-sm btn-circle">
            <span className="icon-[tabler--x] size-4" />
          </button>
        </div>

        <div className="p-4 space-y-4">
          {result !== null ? (
            <div className="text-center py-6">
              <span className="icon-[tabler--check] size-10 text-success mx-auto block mb-2" />
              <div className="text-lg font-semibold text-base-content">Generated {result} draft items</div>
              <p className="text-sm text-base-content/50 mt-1">Review them in the Seasonal tab.</p>
              <button onClick={onCreated} className="btn btn-primary btn-sm mt-4">View Items</button>
            </div>
          ) : (
            <>
              {/* Template */}
              <div>
                <label className="text-xs text-base-content/50 mb-1 block">Template</label>
                {templates.length === 0 ? (
                  <div className="text-sm text-base-content/40">No seasonal templates. Run a sync first to generate defaults.</div>
                ) : (
                  <select value={templateId ?? ''} onChange={e => setTemplateId(Number(e.target.value))} className="select select-sm select-bordered w-full">
                    {templates.map(t => (
                      <option key={t.id} value={t.id}>{t.name} ({t.channel})</option>
                    ))}
                  </select>
                )}
              </div>

              {/* Segment */}
              <div>
                <label className="text-xs text-base-content/50 mb-1 block">Customer Segment</label>
                <div className="space-y-1.5">
                  {segments.map(s => (
                    <label key={s.value} className="flex items-center gap-2 cursor-pointer">
                      <input type="radio" name="segment" value={s.value} checked={segment === s.value} onChange={() => setSegment(s.value)} className="radio radio-sm radio-primary" />
                      <span className="text-sm text-base-content">{s.label}</span>
                    </label>
                  ))}
                </div>
                {segment === 'no_recent_service' && (
                  <div className="mt-2">
                    <label className="text-xs text-base-content/50 mb-1 block">Months</label>
                    <input type="number" value={months} onChange={e => setMonths(Number(e.target.value))} className="input input-sm input-bordered w-24" min={1} />
                  </div>
                )}
              </div>

              <button onClick={handleGenerate} disabled={generating || !templateId} className="btn btn-primary btn-sm w-full gap-1">
                {generating ? <span className="loading loading-spinner loading-xs" /> : <span className="icon-[tabler--speakerphone] size-4" />}
                Generate Campaign
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
