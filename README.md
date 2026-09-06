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

	The API reads `ConnectionStrings:DefaultConnection` at startup. After setting the secret, start the API and test the connection:

	```sh
	dotnet run --project src/FinPlanner.Api
	curl http://localhost:5000/api/health/database
	```

	Use the HTTP and HTTPS ports printed by `dotnet run` if they differ from `5000`. A successful response is:

	```json
	{"status":"connected"}
	```

	The remote PostgreSQL server must allow connections from this machine, and its firewall, SSL mode, database name, username, and permissions must be configured by the database provider.

## Database schema

All application database objects are created in the PostgreSQL `finplanner` schema. The first migration defines the `finplanner.FamilyAccounts` table and stores EF migration history in `finplanner.__EFMigrationsHistory`. Generate migrations from the API project with the EF tool available on the PATH:

```sh
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add MigrationName \
	--project src/FinPlanner.Api \
	--output-dir Data/Migrations
```

Apply pending migrations to the configured remote database:

```sh
dotnet ef database update --project src/FinPlanner.Api
```

The migration is not applied automatically when the API starts.

## Validate

```sh
dotnet build src/FinPlanner.Api
npm run build --prefix src/FinPlanner.Web
```