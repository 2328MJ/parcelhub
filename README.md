# ParcelHub – pipeline sandbox

A small carrier-gateway API for learning CI/CD. It works like a real carrier integration: a client asks
for a shipping label, the API validates and tidies the request, talks to a (fake) carrier, waits for a
tracking number, and returns a label you can open in a browser.

```
POST /api/v1/labels
   │
   1. Validate      RequestValidator    – reports every problem, not just the first
   2. Enhance       RequestEnhancer     – return centre, no-reply email, E.164 phones, NI detection
   3. Carrier       FakeCarrierClient   – create shipment, then poll for the tracking number
   4. Render        LabelRenderer       – SVG (browser) or ZPL (thermal printer)
   │
   └─ errors ──►    ErrorHarmoniser     – standard error codes, original reason kept in "detail"
```

## Run it

```powershell
dotnet test                                     # 42 tests
dotnet run --project src/ParcelHub.Api          # then open http://localhost:5080/swagger
```

Try the samples (from a Git Bash prompt):

```bash
curl -s -X POST localhost:5080/api/v1/labels -H 'Content-Type: application/json' -d @samples/return.json
# copy the trackingNumber from the response, then open the label in a browser:
#   http://localhost:5080/api/v1/labels/PH123456789GB
```

| Sample | What it shows |
|---|---|
| `outbound.json` | Normal delivery; phone converted to +44 format |
| `return.json` | No consignee sent – return centre and no-reply email filled in |
| `return-ni.json` | Northern Ireland return with customs items, ZPL label (view at labelary.com/viewer.html) |
| `validation-errors.json` | Four validation errors reported at once |
| `carrier-error.json` | Passes validation, rejected by the carrier (postcode `ZZ99 9ZZ`) |

Other endpoints: `GET /api/v1/services`, `GET /api/v1/labels/formats`, `GET /health`, `GET /version`.

## Where the ideas come from

| In the InPost service at work | Here |
|---|---|
| Controller → validate → enhance → allocate | `LabelService.CreateLabelAsync` |
| Return codes copied into several classes | One `ServiceCatalogue` everything asks |
| Validation stops at the first error | All errors returned together |
| Consignee email defaulted on returns | `RequestEnhancer` – and each change is listed in `enhancements` |
| Account details written into shared settings | Return centre copied per request (see the test for it) |
| `GetTrackingRefNo` polls and swallows every error | `PollForTrackingNumberAsync` – real errors surface immediately |
| "Unknown Carrier Error" hid the real reason | Unmapped codes still give 10000, but `detail` keeps the cause |
| `/version` didn't exist | Shows which build is running in which environment |

## Things to try

- **Find the unmapped errors.** `TRACKING_TIMEOUT` and `UNEXPECTED` have no entry in `ErrorMappings`.
  Set `FakeCarrier:PollsBeforeTracking` to 50, send a request, and see what the client gets.
- **Make the carrier flaky.** Set `FakeCarrier:FailureRate` to `0.3`. Should `LabelService` retry?
- **Fix a phone edge case.** `"+44 (0)7911 123456"` becomes `+4407911123456`. Write a failing test first.
- **Add a service.** e.g. `PH-NXT` next-day delivery. How many files did you have to touch?

## Jenkins (step 3)

See [`jenkins/README.md`](jenkins/README.md).
