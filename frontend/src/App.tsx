import { lazy, Suspense } from 'react'
import { Route, Routes } from 'react-router-dom'
import { Layout } from './app/Layout'
import { ClientsPage } from './pages/Clients'

const ClientDetailPage = lazy(() => import('./pages/ClientDetail').then((m) => ({ default: m.ClientDetailPage })))
const DashboardPage = lazy(() => import('./pages/Dashboard').then((m) => ({ default: m.DashboardPage })))

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route
          path="/"
          element={
            <Suspense fallback={null}>
              <DashboardPage />
            </Suspense>
          }
        />
        <Route path="/clients" element={<ClientsPage />} />
        <Route
          path="/clients/:mac"
          element={
            <Suspense fallback={null}>
              <ClientDetailPage />
            </Suspense>
          }
        />
      </Route>
    </Routes>
  )
}
