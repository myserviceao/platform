import { useState, useEffect, createContext, useContext, useCallback } from 'react'

export type Theme = 'dark' | 'light' | 'corporate' | 'gourmet' | 'luxury' | 'soft' | 'shadcn' | 'slack' | 'spotify' | 'vscode' | 'claude' | 'pastel' | 'valorant' | 'ghibli' | 'mintlify' | 'perplexity' | 'black'

interface ThemeContextType {
  theme: Theme
  setTheme: (theme: Theme) => Promise<void>
}

export const ThemeContext = createContext<ThemeContextType | null>(null)

export function useTheme() {
  const ctx = useContext(ThemeContext)
  if (!ctx) throw new Error('useTheme must be used inside ThemeProvider')
  return ctx
}

export function useThemeProvider(initialTheme: Theme | null): ThemeContextType {
  const [theme, setThemeState] = useState<Theme>(() => {
    return (initialTheme as Theme) || 'dark'
  })

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
  }, [theme])

  const setTheme = useCallback(async (newTheme: Theme) => {
    setThemeState(newTheme)
    document.documentElement.setAttribute('data-theme', newTheme)
    await fetch('/api/settings/theme', {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ theme: newTheme }),
    })
  }, [])

  return { theme, setTheme }
}
