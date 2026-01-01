/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.cshtml',
    './**/*.cshtml',
    './wwwroot/js/**/*.js'
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          600: 'var(--color-blue-600)',
          700: 'var(--color-blue-700)'
        }
      }
    }
  },
  plugins: []
}
