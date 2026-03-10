import { Link } from 'react-router-dom'
import { Zap, BarChart3, ClipboardList, Users, Bell, ArrowRight, CheckCircle } from 'lucide-react'

const features = [
  {
    icon: BarChart3,
    title: 'Operations Dashboard',
    description: 'KPIs, AR aging, open work orders, and today\'s schedule — all in one command view.',
  },
  {
    icon: ClipboardList,
    title: 'Work Order Board',
    description: 'Kanban board driven by your ServiceTitan hold reasons. Cards auto-populate from synced jobs.',
  },
  {
    icon: Users,
    title: 'Customer Profiles',
    description: 'Full history, equipment, contacts, and invoices — pulled straight from ServiceTitan.',
  },
  {
    icon: Bell,
    title: 'AR Alerts',
    description: 'Aging accounts receivable flagged automatically. Stop chasing invoices manually.',
  },
]

const benefits = [
  'Syncs with ServiceTitan automatically',
  'Multi-user, role-based access',
  'Mobile-friendly on any device',
  'Dark & light theme support',
  'Built for HVAC, plumbing & electrical',
  'No ServiceTitan replacement — an enhancement',
]

export function LandingPage() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      {/* Nav */}
      <header className="border-b border-border/50 sticky top-0 z-50 bg-background/80 backdrop-blur-md">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 flex items-center justify-between h-14">
          <div className="flex items-center gap-2">
            <div className="flex items-center justify-center w-7 h-7 rounded-md bg-primary">
              <Zap className="w-3.5 h-3.5 text-white" />
            </div>
            <span className="font-semibold text-sm text-foreground">MyServiceAO</span>
          </div>
          <div className="flex items-center gap-3">
            <Link
              to="/app/login"
              className="text-sm text-muted-foreground hover:text-foreground transition-colors"
            >
              Sign in
            </Link>
            <Link
              to="/app/register"
              className="text-sm font-medium px-3 py-1.5 rounded-md bg-primary text-white hover:bg-primary/90 transition-colors"
            >
              Get started
            </Link>
          </div>
        </div>
      </header>

      {/* Hero */}
      <section className="max-w-6xl mx-auto px-4 sm:px-6 pt-20 pb-16 text-center">
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-primary/30 bg-primary/5 text-primary text-xs font-medium mb-6">
          <Zap className="w-3 h-3" />
          Built for ServiceTitan contractors
        </div>
        <h1 className="text-4xl sm:text-5xl lg:text-6xl font-bold text-foreground leading-tight mb-5">
          Your operations,
          <br />
          <span className="text-primary">under control.</span>
        </h1>
        <p className="text-lg text-muted-foreground max-w-2xl mx-auto mb-8 leading-relaxed">
          MyServiceAO is a command center layered on top of ServiceTitan.
          Real-time dashboards, work order boards, AR alerts, and more —
          customized for your business.
        </p>
        <div className="flex flex-col sm:flex-row items-center justify-center gap-3">
          <Link
            to="/app/register"
            className="flex items-center gap-2 px-5 py-2.5 rounded-md bg-primary text-white font-medium text-sm hover:bg-primary/90 transition-colors w-full sm:w-auto justify-center"
          >
            Start free trial
            <ArrowRight className="w-4 h-4" />
          </Link>
          <Link
            to="/app/login"
            className="flex items-center gap-2 px-5 py-2.5 rounded-md border border-border text-sm font-medium text-foreground hover:bg-secondary transition-colors w-full sm:w-auto justify-center"
          >
            Sign in to your account
          </Link>
        </div>
      </section>

      {/* Dashboard preview placeholder */}
      <section className="max-w-5xl mx-auto px-4 sm:px-6 pb-20">
        <div className="rounded-xl border border-border bg-card overflow-hidden shadow-2xl">
          <div className="flex items-center gap-2 px-4 py-3 border-b border-border bg-secondary/30">
            <div className="w-3 h-3 rounded-full bg-red-500/70" />
            <div className="w-3 h-3 rounded-full bg-yellow-500/70" />
            <div className="w-3 h-3 rounded-full bg-green-500/70" />
            <span className="ml-3 text-xs text-muted-foreground">myserviceao.com/app/dashboard</span>
          </div>
          <div className="p-6 grid grid-cols-2 sm:grid-cols-4 gap-3">
            {[
              { label: 'Open Work Orders', value: '24', color: 'text-primary' },
              { label: 'AR Balance', value: '$48,200', color: 'text-yellow-400' },
              { label: 'Month Revenue', value: '$182,400', color: 'text-green-400' },
              { label: 'Overdue PMs', value: '7', color: 'text-red-400' },
            ].map((kpi) => (
              <div key={kpi.label} className="bg-background rounded-lg p-4 border border-border">
                <div className={`text-2xl font-bold ${kpi.color}`}>{kpi.value}</div>
                <div className="text-xs text-muted-foreground mt-1">{kpi.label}</div>
              </div>
            ))}
          </div>
          <div className="px-6 pb-6 grid grid-cols-1 sm:grid-cols-3 gap-3">
            {['Dispatched', 'On Hold', 'Needs Parts'].map((col) => (
              <div key={col} className="bg-background rounded-lg border border-border p-3">
                <div className="text-xs font-medium text-muted-foreground mb-2">{col}</div>
                {[1, 2].map((i) => (
                  <div key={i} className="h-10 rounded bg-secondary/60 mb-2 last:mb-0" />
                ))}
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Features */}
      <section className="max-w-6xl mx-auto px-4 sm:px-6 py-16 border-t border-border">
        <div className="text-center mb-12">
          <h2 className="text-3xl font-bold text-foreground mb-3">Everything your team needs</h2>
          <p className="text-muted-foreground">Built on your ServiceTitan data. No double entry.</p>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
          {features.map((f) => (
            <div key={f.title} className="bg-card border border-border rounded-xl p-5 hover:border-primary/40 transition-colors">
              <div className="w-9 h-9 rounded-md bg-primary/10 flex items-center justify-center mb-4">
                <f.icon className="w-4 h-4 text-primary" />
              </div>
              <h3 className="font-semibold text-foreground text-sm mb-1.5">{f.title}</h3>
              <p className="text-xs text-muted-foreground leading-relaxed">{f.description}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Benefits */}
      <section className="max-w-6xl mx-auto px-4 sm:px-6 py-16 border-t border-border">
        <div className="max-w-xl mx-auto text-center mb-10">
          <h2 className="text-3xl font-bold text-foreground mb-3">Built different</h2>
          <p className="text-muted-foreground">Not another tool to manage. An extension of how you already work.</p>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 max-w-2xl mx-auto">
          {benefits.map((b) => (
            <div key={b} className="flex items-center gap-3 text-sm text-muted-foreground">
              <CheckCircle className="w-4 h-4 text-primary shrink-0" />
              {b}
            </div>
          ))}
        </div>
      </section>

      {/* CTA */}
      <section className="max-w-6xl mx-auto px-4 sm:px-6 py-16 border-t border-border text-center">
        <h2 className="text-3xl font-bold text-foreground mb-3">Ready to take control?</h2>
        <p className="text-muted-foreground mb-8 max-w-md mx-auto">
          Set up your account in minutes. Connect ServiceTitan and your dashboard is live.
        </p>
        <Link
          to="/app/register"
          className="inline-flex items-center gap-2 px-6 py-3 rounded-md bg-primary text-white font-medium hover:bg-primary/90 transition-colors"
        >
          Get started free
          <ArrowRight className="w-4 h-4" />
        </Link>
      </section>

      {/* Footer */}
      <footer className="border-t border-border py-8">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 flex flex-col sm:flex-row items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <div className="flex items-center justify-center w-6 h-6 rounded bg-primary">
              <Zap className="w-3 h-3 text-white" />
            </div>
            <span className="text-sm font-medium text-foreground">MyServiceAO</span>
          </div>
          <p className="text-xs text-muted-foreground">
            © {new Date().getFullYear()} MyServiceAO. All rights reserved.
          </p>
          <div className="flex gap-4 text-xs text-muted-foreground">
            <a href="#" className="hover:text-foreground transition-colors">Privacy</a>
            <a href="#" className="hover:text-foreground transition-colors">Terms</a>
            <a href="#" className="hover:text-foreground transition-colors">Contact</a>
          </div>
        </div>
      </footer>
    </div>
  )
}
