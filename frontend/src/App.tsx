import { lazy, Suspense } from 'react'
import { Route, Routes } from 'react-router-dom'
import { Layout } from './app/Layout'
import { OverviewPage } from './pages/Overview'

const ClientDetailPage = lazy(() => import('./pages/ClientDetail').then((m) => ({ default: m.ClientDetailPage })))

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<OverviewPage />} />
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
