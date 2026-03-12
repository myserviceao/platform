import { useState, useEffect } from 'react'

interface WeatherDay {
  date: string
  dayName: string
  high: number
  low: number
  code: number
  precip: number
}

interface CurrentWeather {
  temp: number
  code: number
  wind: number
  humidity: number
  feelsLike: number
}

const WMO: Record<number, { label: string; icon: string }> = {
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
  80: { label: 'Showers', icon: 'icon-[tabler--cloud-rain]' },
  81: { label: 'Showers', icon: 'icon-[tabler--cloud-storm]' },
  82: { label: 'Heavy Showers', icon: 'icon-[tabler--cloud-storm]' },
  95: { label: 'Thunderstorm', icon: 'icon-[tabler--cloud-bolt]' },
  96: { label: 'T-storm + Hail', icon: 'icon-[tabler--cloud-bolt]' },
  99: { label: 'T-storm + Hail', icon: 'icon-[tabler--cloud-bolt]' },
}

function wmo(code: number) { return WMO[code] || { label: 'Unknown', icon: 'icon-[tabler--cloud]' } }

function dayLabel(dateStr: string, idx: number) {
  if (idx === 0) return 'Today'
  if (idx === 1) return 'Tomorrow'
  return new Date(dateStr + 'T12:00:00').toLocaleDateString('en-US', { weekday: 'short' })
}

const LAT = 29.76, LON = -95.37

export function WeatherCard() {
  const [current, setCurrent] = useState<CurrentWeather | null>(null)
  const [forecast, setForecast] = useState<WeatherDay[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch(`https://api.open-meteo.com/v1/forecast?latitude=${LAT}&longitude=${LON}&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&temperature_unit=fahrenheit&wind_speed_unit=mph&timezone=America%2FChicago&forecast_days=4`)
      .then(r => r.json())
      .then(d => {
        setCurrent({
          temp: Math.round(d.current.temperature_2m),
          code: d.current.weather_code,
          wind: Math.round(d.current.wind_speed_10m),
          humidity: d.current.relative_humidity_2m,
          feelsLike: Math.round(d.current.apparent_temperature),
        })
        setForecast(d.daily.time.slice(0, 4).map((date: string, i: number) => ({
          date, dayName: dayLabel(date, i),
          high: Math.round(d.daily.temperature_2m_max[i]),
          low: Math.round(d.daily.temperature_2m_min[i]),
          code: d.daily.weather_code[i],
          precip: d.daily.precipitation_probability_max[i],
        })))
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  if (loading) {
    return (
      <div className="card bg-base-100 shadow-sm h-full">
        <div className="card-body p-4 flex items-center justify-center">
          <span className="loading loading-spinner loading-sm text-base-content/30" />
        </div>
      </div>
    )
  }

  if (!current) return null
  const info = wmo(current.code)

  return (
    <div className="card bg-base-100 shadow-sm h-full">
      <div className="card-body p-0">
        {/* Header */}
        <div className="flex items-center justify-between px-4 pt-4 pb-2">
          <div className="flex items-center gap-2">
            <span className="icon-[tabler--map-pin] size-4 text-base-content/40" />
            <span className="text-sm font-semibold text-base-content">Houston, TX</span>
          </div>
          <span className="text-[10px] text-base-content/20">Open-Meteo</span>
        </div>

        {/* Current */}
        <div className="flex items-center gap-4 px-4 pb-3 border-b border-base-200">
          <div className="flex items-center gap-3">
            <span className={`${info.icon} size-10 text-info`} />
            <div>
              <div className="text-3xl font-bold text-base-content leading-none">{current.temp}°</div>
              <div className="text-xs text-base-content/50 mt-0.5">{info.label}</div>
            </div>
          </div>
          <div className="ml-auto text-right space-y-0.5">
            <div className="text-xs text-base-content/50">Feels {current.feelsLike}°</div>
            <div className="text-xs text-base-content/50">
              <span className="icon-[tabler--droplet] size-3 inline-block align-middle mr-0.5" />{current.humidity}%
            </div>
            <div className="text-xs text-base-content/50">
              <span className="icon-[tabler--wind] size-3 inline-block align-middle mr-0.5" />{current.wind} mph
            </div>
          </div>
        </div>

        {/* Forecast */}
        <div className="grid grid-cols-4 gap-1 px-3 py-3">
          {forecast.map((day, i) => {
            const di = wmo(day.code)
            return (
              <div key={day.date} className={'text-center rounded-lg py-2 px-1' + (i === 0 ? ' bg-base-content/5' : '')}>
                <div className="text-xs font-medium text-base-content/60 mb-1.5">{day.dayName}</div>
                <span className={`${di.icon} size-6 text-base-content/40 block mx-auto mb-1.5`} />
                <div className="text-sm font-bold text-base-content">{day.high}°</div>
                <div className="text-xs text-base-content/30">{day.low}°</div>
                {day.precip > 0 && (
                  <div className="text-[10px] text-info mt-1">
                    <span className="icon-[tabler--droplet] size-2.5 inline-block align-middle" /> {day.precip}%
                  </div>
                )}
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
