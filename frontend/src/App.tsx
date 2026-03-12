import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthContext, useAuthProvider } from '@/hooks/useAuth'
import { ThemeContext, useThemeProvider, type Theme } from '@/hooks/useTheme'
import { LoginPage } from '@/pages/App/LoginPage'
import { RegisterPage } from '@/pages/App/RegisterPage'
import { DashboardPage } from '@/pages/App/Dashboard/DashboardPage'
import { PmTrackerPage } from '@/pages/App/PmTracker/PmTrackerPage'
import { SettingsPage } from '@/pages/App/Settings/SettingsPage'
import { CashFlowPage } from '@/pages/App/CashFlow/CashFlowPage'
import { AppShell } from '@/components/layout/AppShell'
import ServiceTitanPage from '@/pages/App/ServiceTitanPage'
import { CustomersPage } from '@/pages/App/Customers/CustomersPage'
import { CustomerDetailPage } from '@/pages/App/Customers/CustomerDetailPage'
import { ApPage } from '@/pages/App/Ap/ApPage'
import { PmOutreachPage } from '@/pages/App/PmOutreach/PmOutreachPage'
import { PmPlannerPage } from '@/pages/App/PmPlanner/PmPlannerPage'
import { WoBoardPage } from '@/pages/App/WoBoard/WoBoardPage'
import { HoldBoardPage } from '@/pages/App/HoldBoard/HoldBoardPage'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const auth = useAuthProvider()
  if (auth.loading) {
    return (
      <div className="min-h-screen bg-base-200 flex items-center justify-center">
        <span className="loading loading-spinner loading-md text-primary" />
      </div>
    )
  }
  if (!auth.user) return <Navigate to="/app/login" replace />
  return <>{children}</>
}

function AppRoutes() {
  const auth = useAuthProvider()
  const themeProvider = useThemeProvider((auth.user?.theme as Theme) ?? 'black')

  return (
    <AuthContext.Provider value={auth}>
      <ThemeContext.Provider value={themeProvider}>
        <BrowserRouter>
          <Routes>
            <Route path="/" element={<Navigate to="/app/login" replace />} />
            <Route path="/app/login" element={<LoginPage />} />
            <Route path="/app/register" element={<RegisterPage />} />

            <Route
              path="/app/*"
              element={
                <ProtectedRoute>
                  <AppShell>
                    <Routes>
                      <Route index element={<Navigate to="/app/dashboard" replace />} />
                      <Route path="dashboard" element={<DashboardPage />} />
                      <Route path="pm-tracker" element={<PmTrackerPage />} />
                      <Route path="pm-outreach" element={<PmOutreachPage />} />
                      <Route path="pm-planner" element={<PmPlannerPage />} />
                      <Route path="wo-board" element={<WoBoardPage />} />
                      <Route path="hold-board" element={<HoldBoardPage />} />
                      <Route path="ap" element={<ApPage />} />
                      <Route path="cash-flow" element={<CashFlowPage />} />
                      <Route path="settings" element={<SettingsPage />} />
                      <Route path="customers" element={<CustomersPage />} />
                      <Route path="customers/:id" element={<CustomerDetailPage />} />
                      <Route path="servicetitan" element={<ServiceTitanPage />} />
                      <Route path="*" element={
                        <div className="flex items-center justify-center h-64 text-base-content/40 text-sm">
                          Coming soon...
                        </div>
                      } />
                    </Routes>
                  </AppShell>
                </ProtectedRoute>
              }
            />

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </ThemeContext.Provider>
    </AuthContext.Provider>
  )
}

export default AppRoutes
