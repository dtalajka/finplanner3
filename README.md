# FinPlanner

Family finance management with an ASP.NET Core API and a React TypeScript frontend.

## Structure

- `src/FinPlanner.Api` - ASP.NET Core API targeting .NET 10
- `src/FinPlanner.Web` - React TypeScript frontend powered by Vite
- `FinPlanner.slnx` - .NET solution

## Run locally

Start the API:

```sh
dotnet run --project src/FinPlanner.Api
```

Start the frontend in another terminal:

```sh
npm install --prefix src/FinPlanner.Web
npm run dev --prefix src/FinPlanner.Web
```

The application will use a remote PostgreSQL database. Connection details belong in environment variables or .NET user secrets, never in source control:

```sh
dotnet user-secrets init --project src/FinPlanner.Api
dotnet user-secrets set --project src/FinPlanner.Api \
	"ConnectionStrings:DefaultConnection" \
	"Host=your-host;Database=your-database;Username=your-user;Password=your-password"
```

## Validate

```sh
dotnet build src/FinPlanner.Api
npm run build --prefix src/FinPlanner.Web
```