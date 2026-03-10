import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthContext, useAuthProvider } from '@/hooks/useAuth'
import { LandingPage } from '@/pages/Landing/LandingPage'
import { LoginPage } from '@/pages/App/LoginPage'
import { RegisterPage } from '@/pages/App/RegisterPage'
import { DashboardPage } from '@/pages/App/Dashboard/DashboardPage'
import { AppShell } from '@/components/layout/AppShell'
import ServiceTitanPage from '@/pages/App/ServiceTitanPage'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const auth = useAuthProvider()
  if (auth.loading) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <div className="w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin" />
      </div>
    )
  }
  if (!auth.user) return <Navigate to="/app/login" replace />
  return <>{children}</>
}

function AppRoutes() {
  const auth = useAuthProvider()

  return (
    <AuthContext.Provider value={auth}>
      <BrowserRouter>
        <Routes>
          {/* Public */}
          <Route path="/" element={<LandingPage />} />
          <Route path="/app/login" element={<LoginPage />} />
          <Route path="/app/register" element={<RegisterPage />} />

          {/* Protected */}
          <Route
            path="/app/*"
            element={
              <ProtectedRoute>
                <AppShell>
                  <Routes>
                    <Route index element={<Navigate to="/app/dashboard" replace />} />
                    <Route path="dashboard" element={<DashboardPage />} />
                    <Route path="servicetitan" element={<ServiceTitanPage />} />
                    <Route path="*" element={<div className="text-muted-foreground text-sm">Coming soon...</div>} />
                  </Routes>
                </AppShell>
              </ProtectedRoute>
            }
          />

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthContext.Provider>
  )
}

export default AppRoutes