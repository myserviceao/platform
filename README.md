# MyServiceAO

**My Service AO** — The operations platform for service contractors.

Built on top of ServiceTitan data. A command center, not a replacement.

## Stack

- **Backend**: ASP.NET Core (.NET 10) + PostgreSQL + Entity Framework Core
- **Frontend**: React + TypeScript (Vite) + shadcn/ui + Tailwind CSS
- **Auth**: Cookie-based sessions + BCrypt
- **Hosting**: Railway (single service, backend serves frontend build)
- **Integration**: ServiceTitan API (OAuth2)

## Project Structure

```
myserviceao/
├── backend/         # ASP.NET Core Web API
├── frontend/        # React + TypeScript (Vite)
└── .github/         # CI/CD workflows
```

## Local Development

### Backend
```bash
cd backend
dotnet run
```

### Frontend
```bash
cd frontend
npm install
npm run dev
```

## Deployment

Deploys automatically to Railway on push to `main`.

## Environment Variables

### Backend (Railway)
```
DATABASE_URL=postgresql://...
SESSION_SECRET=...
ASPNETCORE_ENVIRONMENT=Production
```

### Frontend
Built and served by the backend in production. Uses `VITE_API_URL` for local dev.
