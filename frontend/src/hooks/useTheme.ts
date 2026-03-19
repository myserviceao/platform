import { useState, useEffect, createContext, useContext, useCallback, useRef } from 'react'

export type Theme = 'dark' | 'light' | 'corporate' | 'gourmet' | 'luxury' | 'soft' | 'shadcn' | 'slack' | 'spotify' | 'vscode' | 'claude' | 'pastel' | 'valorant' | 'ghibli' | 'mintlify' | 'perplexity' | 'black' | 'asap'

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

  // When initialTheme changes (e.g. auth loads), update theme
  const hasLoadedFromAuth = useRef(false)
  useEffect(() => {
    if (initialTheme && !hasLoadedFromAuth.current) {
      hasLoadedFromAuth.current = true
      setThemeState(initialTheme)
      document.documentElement.setAttribute('data-theme', initialTheme)
    }
  }, [initialTheme])

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
