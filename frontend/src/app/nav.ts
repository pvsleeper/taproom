import { LayoutGrid, type LucideIcon } from 'lucide-react'

export interface NavItem {
  label: string
  path: string
  icon: LucideIcon
}

export const navItems: NavItem[] = [
  { label: 'Overview', path: '/', icon: LayoutGrid },
]
