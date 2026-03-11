import type { Config } from 'tailwindcss'

export default {
  content: [
    './index.html',
    './src/**/*.{ts,tsx}',
    './node_modules/flyonui/dist/js/*.js',
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
    },
  },
  plugins: [
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    require('flyonui'),
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    require('flyonui/plugin'),
  ],
  flyonui: {
    themes: ['dark', 'light', 'corporate', 'gourmet', 'luxury', 'soft', 'sunset', 'forest', 'synthwave', 'dracula', 'night', 'dim'],
  },
} satisfies Config
