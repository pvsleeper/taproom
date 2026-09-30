import { Menu, PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { navItems } from './nav'

export function Layout() {
  const [collapsed, setCollapsed] = useState(false)
  const [drawerOpen, setDrawerOpen] = useState(false)

  return (
    <div className="flex h-dvh overflow-hidden bg-bg text-text">
      {drawerOpen && (
        <button
          aria-label="Close navigation"
          className="fixed inset-0 z-20 bg-black/60 md:hidden"
          onClick={() => setDrawerOpen(false)}
        />
      )}

      <aside
        className={`
          fixed inset-y-0 left-0 z-30 flex flex-col border-r border-border bg-surface
          transition-transform duration-200 md:static md:translate-x-0
          ${drawerOpen ? 'translate-x-0' : '-translate-x-full'}
          ${collapsed ? 'md:w-16' : 'md:w-60'} w-60
        `}
      >
        <div className="flex h-14 items-center justify-between border-b border-border px-4">
          {!collapsed && (
            <span className="text-lg font-semibold tracking-tight text-text">
              Tap<span className="text-accent">room</span>
            </span>
          )}
          <button
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            className="hidden rounded p-1.5 text-text-muted hover:bg-surface-hover hover:text-text md:block"
            onClick={() => setCollapsed((c) => !c)}
          >
            {collapsed ? <PanelLeftOpen size={18} /> : <PanelLeftClose size={18} />}
          </button>
        </div>

        <nav className="flex-1 space-y-1 p-2">
          {navItems.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              end={item.path === '/'}
              onClick={() => setDrawerOpen(false)}
              className={({ isActive }) => `
                flex items-center gap-3 rounded-md border-l-2 px-3 py-2 text-sm font-medium transition-colors
                ${isActive
                  ? 'border-accent bg-surface-hover text-text'
                  : 'border-transparent text-text-muted hover:bg-surface-hover hover:text-text'}
              `}
            >
              <item.icon size={18} className="shrink-0" />
              {!collapsed && <span>{item.label}</span>}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex h-14 items-center gap-3 border-b border-border px-4 md:hidden">
          <button
            aria-label="Open navigation"
            className="rounded p-1.5 text-text-muted hover:bg-surface-hover hover:text-text"
            onClick={() => setDrawerOpen(true)}
          >
            <Menu size={20} />
          </button>
          <span className="text-base font-semibold">
            Tap<span className="text-accent">room</span>
          </span>
        </div>

        <main className="min-h-0 flex-1 overflow-y-auto">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
