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

All application database objects are created in the PostgreSQL `finplanner` schema. The EF Core migration is the application’s schema mechanism, with [final-model.sql](final-model.sql) retained as the reference definition. It defines accounts, categories, recurring rules, planned transactions, actual transactions, and the transaction timeline view. Actual transactions use `from_account_id` and `to_account_id`: source only is an expense, destination only is income, and both are a transfer.

Apply pending application migrations to the configured remote database:

```sh
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef database update --project src/FinPlanner.Api
```

The API does not apply migrations automatically when it starts. To review the SQL first:

```sh
dotnet ef migrations script --project src/FinPlanner.Api --output finplanner-migrations.sql
```

## Validate

```sh
dotnet build src/FinPlanner.Api
npm run build --prefix src/FinPlanner.Web
```

## API

The frontend uses `http://localhost:5140/api` by default. Set `VITE_API_URL` before starting Vite when the API uses another URL:

```sh
VITE_API_URL=http://localhost:5140/api npm run dev --prefix src/FinPlanner.Web
```

Available endpoints:

- `GET /api/accounts` - list accounts
- `POST /api/accounts` - create an account
- `GET /api/accounts/{id}` - read one account
- `PUT /api/accounts/{id}` - update an account
- `DELETE /api/accounts/{id}` - deactivate an account
- `GET /api/categories` - list active categories
- `POST /api/categories` - create a category
- `GET /api/planning/planned-transactions?from=YYYY-MM-DD&to=YYYY-MM-DD` - list planned transactions
- `POST /api/planning/planned-transactions` - create a planned transaction
- `GET /api/planning/recurring-rules` - list recurring rules
- `POST /api/planning/recurring-rules` - create a recurring rule
- `GET /api/transactions?from=YYYY-MM-DD&to=YYYY-MM-DD&type=Income|Expense` - list actual transactions
- `GET /api/transactions/{id}` - read one actual transaction
- `POST /api/transactions` - create an actual transaction
- `PUT /api/transactions/{id}` - update an actual transaction
- `DELETE /api/transactions/{id}` - delete an actual transaction

Create at least one active account before adding transactions. The transaction screen loads accounts from the API and uses the selected account for every create or update.
