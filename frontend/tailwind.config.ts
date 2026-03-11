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
<<<<<<< HEAD
=======
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    require('flyonui/plugin'),
>>>>>>> 622fa3a658643b805f9a000b7b37bfa2a38411d5
  ],
  flyonui: {
    themes: ['dark', 'light', 'corporate'],
  },
} satisfies Config
