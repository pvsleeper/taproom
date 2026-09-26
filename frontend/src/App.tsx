import { Route, Routes } from 'react-router-dom'
import { Layout } from './app/Layout'
import { OverviewPage } from './pages/Overview'

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<OverviewPage />} />
      </Route>
    </Routes>
  )
}
