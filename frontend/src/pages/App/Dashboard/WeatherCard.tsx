import { useState, useEffect } from 'react'

interface WeatherDay {
  date: string
  dayName: string
  high: number
  low: number
  code: number
  precip: number
  wind: number
  humidity: number
}

interface CurrentWeather {
  temp: number
  code: number
  wind: number
  humidity: number
  feelsLike: number
}

const WMO_CODES: Record<number, { label: string; icon: string }> = {
  0: { label: 'Clear', icon: 'icon-[tabler--sun]' },
  1: { label: 'Mostly Clear', icon: 'icon-[tabler--sun]' },
  2: { label: 'Partly Cloudy', icon: 'icon-[tabler--cloud-sun]' },
  3: { label: 'Overcast', icon: 'icon-[tabler--cloud]' },
  45: { label: 'Foggy', icon: 'icon-[tabler--mist]' },
  48: { label: 'Rime Fog', icon: 'icon-[tabler--mist]' },
  51: { label: 'Light Drizzle', icon: 'icon-[tabler--cloud-rain]' },
  53: { label: 'Drizzle', icon: 'icon-[tabler--cloud-rain]' },
  55: { label: 'Heavy Drizzle', icon: 'icon-[tabler--cloud-rain]' },
  61: { label: 'Light Rain', icon: 'icon-[tabler--cloud-rain]' },
  63: { label: 'Rain', icon: 'icon-[tabler--cloud-storm]' },
  65: { label: 'Heavy Rain', icon: 'icon-[tabler--cloud-storm]' },
  71: { label: 'Light Snow', icon: 'icon-[tabler--snowflake]' },
  73: { label: 'Snow', icon: 'icon-[tabler--snowflake]' },
  75: { label: 'Heavy Snow', icon: 'icon-[tabler--snowflake]' },
  77: { label: 'Snow Grains', icon: 'icon-[tabler--snowflake]' },
  80: { label: 'Light Showers', icon: 'icon-[tabler--cloud-rain]' },
  81: { label: 'Showers', icon: 'icon-[tabler--cloud-storm]' },
  82: { label: 'Heavy Showers', icon: 'icon-[tabler--cloud-storm]' },
  85: { label: 'Snow Showers', icon: 'icon-[tabler--snowflake]' },
  86: { label: 'Heavy Snow Showers', icon: 'icon-[tabler--snowflake]' },
  95: { label: 'Thunderstorm', icon: 'icon-[tabler--cloud-bolt]' },
  96: { label: 'Thunderstorm + Hail', icon: 'icon-[tabler--cloud-bolt]' },
  99: { label: 'Thunderstorm + Hail', icon: 'icon-[tabler--cloud-bolt]' },
}

function getWeatherInfo(code: number) {
  return WMO_CODES[code] || { label: 'Unknown', icon: 'icon-[tabler--cloud]' }
}

function getDayName(dateStr: string, idx: number) {
  if (idx === 0) return 'Today'
  if (idx === 1) return 'Tomorrow'
  return new Date(dateStr + 'T12:00:00').toLocaleDateString('en-US', { weekday: 'short' })
}

// Houston, TX coordinates (default for HVAC company)
const LAT = 29.76
const LON = -95.37

export function WeatherCard() {
  const [current, setCurrent] = useState<CurrentWeather | null>(null)
  const [forecast, setForecast] = useState<WeatherDay[]>([])
  const [loading, setLoading] = useState(true)
  const [locationName] = useState('Houston, TX')

  useEffect(() => {
    const url = `https://api.open-meteo.com/v1/forecast?latitude=${LAT}&longitude=${LON}&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max,relative_humidity_2m_mean&temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch&timezone=America%2FChicago&forecast_days=4`

    fetch(url)
      .then(r => r.json())
      .then(d => {
        setCurrent({
          temp: Math.round(d.current.temperature_2m),
          code: d.current.weather_code,
          wind: Math.round(d.current.wind_speed_10m),
          humidity: d.current.relative_humidity_2m,
          feelsLike: Math.round(d.current.apparent_temperature),
        })

        const days: WeatherDay[] = d.daily.time.slice(0, 4).map((date: string, i: number) => ({
          date,
          dayName: getDayName(date, i),
          high: Math.round(d.daily.temperature_2m_max[i]),
          low: Math.round(d.daily.temperature_2m_min[i]),
          code: d.daily.weather_code[i],
          precip: d.daily.precipitation_probability_max[i],
          wind: Math.round(d.daily.wind_speed_10m_max[i]),
          humidity: Math.round(d.daily.relative_humidity_2m_mean[i]),
        }))
        setForecast(days)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  if (loading) {
    return (
      <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
        <div className="flex items-center gap-2 mb-3">
          <span className="icon-[tabler--cloud-sun] size-5 text-info" />
          <h3 className="font-semibold text-sm">Weather</h3>
        </div>
        <div className="flex items-center justify-center py-6">
          <span className="loading loading-spinner loading-sm text-base-content/30" />
        </div>
      </div>
    )
  }

  if (!current) return null

  const info = getWeatherInfo(current.code)

  return (
    <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <span className="icon-[tabler--map-pin] size-4 text-base-content/40" />
          <span className="text-xs text-base-content/60">{locationName}</span>
        </div>
        <span className="text-[10px] text-base-content/30">Open-Meteo</span>
      </div>

      {/* Current conditions */}
      <div className="flex items-center gap-4 mb-4">
        <div className="flex items-center gap-3">
          <span className={`${info.icon} size-10 text-info`} />
          <div>
            <div className="text-3xl font-bold text-base-content leading-none">{current.temp}°</div>
            <div className="text-xs text-base-content/50 mt-0.5">{info.label}</div>
          </div>
        </div>
        <div className="ml-auto text-right space-y-0.5">
          <div className="text-xs text-base-content/50">Feels like {current.feelsLike}°</div>
          <div className="text-xs text-base-content/50">
            <span className="icon-[tabler--droplet] size-3 inline-block align-middle mr-0.5" />
            {current.humidity}%
          </div>
          <div className="text-xs text-base-content/50">
            <span className="icon-[tabler--wind] size-3 inline-block align-middle mr-0.5" />
            {current.wind} mph
          </div>
        </div>
      </div>

      {/* 3-day forecast */}
      <div className="grid grid-cols-4 gap-2 border-t border-base-content/10 pt-3">
        {forecast.map((day, i) => {
          const dayInfo = getWeatherInfo(day.code)
          return (
            <div key={day.date} className={'text-center rounded-lg py-2 px-1' + (i === 0 ? ' bg-base-content/5' : '')}>
              <div className="text-xs font-medium text-base-content/70 mb-1">{day.dayName}</div>
              <span className={`${dayInfo.icon} size-5 text-base-content/50 block mx-auto mb-1`} />
              <div className="text-sm font-semibold text-base-content">{day.high}°</div>
              <div className="text-xs text-base-content/40">{day.low}°</div>
              {day.precip > 0 && (
                <div className="text-[10px] text-info mt-0.5">
                  <span className="icon-[tabler--droplet] size-2.5 inline-block align-middle" />
                  {day.precip}%
                </div>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
