# Smart Document Processing System

Live demo deployed app can be accessed and used at: https://sdps.onrender.com, keep in mind it won't be up for a long time.
End-to-end document processing demo for invoices and purchase orders. The app ingests PDF, TXT, CSV, and image files, extracts structured fields, validates the result, and provides a review UI for corrections and confirmation.

## Stack

- Backend: ASP.NET Core 9, EF Core, PostgreSQL
- Frontend: Angular 21
- OCR: Tesseract CLI, optional
- Sample data: `sample-data/resources`

## Current Capabilities

- Upload files through the UI.
- Batch import the provided sample dataset.
- Extract generated ReportLab PDF invoices and purchase orders.
- Parse TXT invoice summaries.
- Parse CSV line items.
- Attempt OCR for PNG/JPG files when Tesseract is installed.
- Persist documents, line items, and validation issues.
- Review and edit extracted data.
- Revalidate, confirm, or reject documents.
- Dashboard summary with status counts, issue counts, and totals by currency.

## Validation Rules

Errors block validation:

- Missing document number
- Duplicate document number
- Due date before issue date
- Line total mismatch
- Subtotal mismatch
- Total mismatch
- Tax mismatch

Warnings can be accepted after review:

- Missing supplier
- Missing issue date
- Missing currency
- Missing line items for minimal TXT/image files
- Unknown document type
- OCR text could not be extracted because Tesseract is missing

## Backend Setup

Install .NET 9 SDK and PostgreSQL.

Update the connection string in `SmartDocumentProcessingSystemBackend/appsettings.Development.json` if your local PostgreSQL credentials differ:

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=sdps;Username=postgres;Password=1234"
```

Restore/build:

```powershell
dotnet restore SmartDocumentProcessingSystemBackend\SmartDocumentProcessingSystem.sln
dotnet build SmartDocumentProcessingSystemBackend\SmartDocumentProcessingSystem.sln
```

Create/update the database:

```powershell
dotnet ef database update --project SmartDocumentProcessingSystemBackend\SmartDocumentProcessingSystem.csproj --startup-project SmartDocumentProcessingSystemBackend\SmartDocumentProcessingSystem.csproj
```

Run the backend:

```powershell
dotnet run --project SmartDocumentProcessingSystemBackend\SmartDocumentProcessingSystem.csproj --launch-profile http
```

The API runs at `http://localhost:5183`.

## Frontend Setup

```powershell
cd SmartDocumentProcessingSystemFrontend
npm install
npm.cmd start
```

The UI runs at `http://localhost:4200`.

## OCR Setup

Tesseract is not committed to the repo. Install it on the host machine and make sure the `tesseract` command is available on `PATH`.

If the command name/path differs, update:

```json
"Processing": {
  "TesseractCommand": "tesseract"
}
```

When Tesseract is missing, image processing still completes, but the document receives a warning that OCR text could not be extracted.

## Useful API Endpoints

- `GET /api/documents`
- `GET /api/documents/summary`
- `GET /api/documents/{id}`
- `POST /api/documents/upload`
- `POST /api/documents/import-samples`
- `PATCH /api/documents/{id}`
- `POST /api/documents/{id}/validate`
- `POST /api/documents/{id}/confirm`
- `POST /api/documents/{id}/reject`

## Tests

The lightweight parser/validator test runner avoids extra test-framework dependencies:

```powershell
dotnet run --project SmartDocumentProcessingSystemBackend.Tests\SmartDocumentProcessingSystemBackend.Tests.csproj
```

## Deployment

PostgreSQL was chosen over initial MySQL to make deployment easier.

## AI Tools Used

Codex was used for codebase analysis, implementation assistance, and validation of certain logic.

## Known Limitations

- OCR depends on local Tesseract installation.
- Messy screenshot-style invoice extraction is best-effort OCR and will require review.
- CSV samples do not contain document metadata, so missing document number/supplier/date/currency warnings or errors are expected.
