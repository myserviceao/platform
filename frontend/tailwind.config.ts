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
  ],
  flyonui: {
    themes: ['dark', 'light', 'corporate'],
  },
} satisfies Config
