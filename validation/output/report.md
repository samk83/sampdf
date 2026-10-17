# Stage 0 validation run

- Engine: PDFium + PDFsharp (PDFsharp 6.2.4.0)
- Runtime: .NET 8.0.32, Ubuntu 22.04.5 LTS
- Run at: 2026-10-17 04:28 +00:00

## Files

- `samples/virtual/plan-set.pdf` — 13.4 KB
- `samples/virtual/dense-vector.pdf` — 196.6 KB
- `validation/output/markup-roundtrip.pdf` — 53.0 KB

## Markups after save → reopen

| Page | Rotation | Kind | PDF subtype | Author | Subject | Comment |
|---|---|---|---|---|---|---|
| 1 | 0 | Other | Text | Other Reviewer | Existing Note | Existing reviewer note — keep me. |
| 1 | 0 | Cloud | Polygon | Sam | Structural | Cloud: revision area |
| 1 | 0 | Callout | FreeText | Sam | Structural | VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM. |
| 1 | 0 | TextBox | FreeText | Sam | Structural | 한글 주석: 간섭 확인 필요 |
| 1 | 0 | Line | Line | Sam | Structural | Line |
| 1 | 0 | Arrow | Line | Sam | Structural | Arrow |
| 1 | 0 | Rectangle | Square | Sam | Structural | Rectangle |
| 1 | 0 | Ellipse | Circle | Sam | Structural | Ellipse |
| 1 | 0 | Ink | Ink | Sam | Structural | Pen |
| 1 | 0 | Highlight | Square | Sam | Structural | Highlight |
| 2 | 90 | Cloud | Polygon | Sam | Structural | Cloud: revision area |
| 2 | 90 | Callout | FreeText | Sam | Structural | VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM. |
| 2 | 90 | TextBox | FreeText | Sam | Structural | 한글 주석: 간섭 확인 필요 |
| 2 | 90 | Line | Line | Sam | Structural | Line |
| 2 | 90 | Arrow | Line | Sam | Structural | Arrow |
| 2 | 90 | Rectangle | Square | Sam | Structural | Rectangle |
| 2 | 90 | Ellipse | Circle | Sam | Structural | Ellipse |
| 2 | 90 | Ink | Ink | Sam | Structural | Pen |
| 2 | 90 | Highlight | Square | Sam | Structural | Highlight |
| 3 | 0 | Cloud | Polygon | Sam | Structural | Cloud: revision area |
| 3 | 0 | Callout | FreeText | Sam | Structural | VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM. |
| 3 | 0 | TextBox | FreeText | Sam | Structural | 한글 주석: 간섭 확인 필요 |
| 3 | 0 | Line | Line | Sam | Structural | Line |
| 3 | 0 | Arrow | Line | Sam | Structural | Arrow |
| 3 | 0 | Rectangle | Square | Sam | Structural | Rectangle |
| 3 | 0 | Ellipse | Circle | Sam | Structural | Ellipse |
| 3 | 0 | Ink | Ink | Sam | Structural | Pen |
| 3 | 0 | Highlight | Square | Sam | Structural | Highlight |
| 4 | 0 | Cloud | Polygon | Sam | Structural | Cloud: revision area |
| 4 | 0 | Callout | FreeText | Sam | Structural | VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM. |
| 4 | 0 | TextBox | FreeText | Sam | Structural | 한글 주석: 간섭 확인 필요 |
| 4 | 0 | Line | Line | Sam | Structural | Line |
| 4 | 0 | Arrow | Line | Sam | Structural | Arrow |
| 4 | 0 | Rectangle | Square | Sam | Structural | Rectangle |
| 4 | 0 | Ellipse | Circle | Sam | Structural | Ellipse |
| 4 | 0 | Ink | Ink | Sam | Structural | Pen |
| 4 | 0 | Highlight | Square | Sam | Structural | Highlight |
| 5 | 180 | Cloud | Polygon | Sam | Structural | Cloud: revision area |
| 5 | 180 | Callout | FreeText | Sam | Structural | VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM. |
| 5 | 180 | TextBox | FreeText | Sam | Structural | 한글 주석: 간섭 확인 필요 |
| 5 | 180 | Line | Line | Sam | Structural | Line |
| 5 | 180 | Arrow | Line | Sam | Structural | Arrow |
| 5 | 180 | Rectangle | Square | Sam | Structural | Rectangle |
| 5 | 180 | Ellipse | Circle | Sam | Structural | Ellipse |
| 5 | 180 | Ink | Ink | Sam | Structural | Pen |
| 5 | 180 | Highlight | Square | Sam | Structural | Highlight |
| 6 | 270 | Cloud | Polygon | Sam | Structural | Cloud: revision area |
| 6 | 270 | Callout | FreeText | Sam | Structural | VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM. |
| 6 | 270 | TextBox | FreeText | Sam | Structural | 한글 주석: 간섭 확인 필요 |
| 6 | 270 | Line | Line | Sam | Structural | Line |
| 6 | 270 | Arrow | Line | Sam | Structural | Arrow |
| 6 | 270 | Rectangle | Square | Sam | Structural | Rectangle |
| 6 | 270 | Ellipse | Circle | Sam | Structural | Ellipse |
| 6 | 270 | Ink | Ink | Sam | Structural | Pen |
| 6 | 270 | Highlight | Square | Sam | Structural | Highlight |

## Performance (reference only — re-measure on the Windows reference PC)

| File | Open | Fit-to-screen render (1600px) | 400% tile render |
|---|---|---|---|
| plan-set.pdf | 1 ms | 23 ms | 21 ms |
| dense-vector.pdf | 0 ms | 218 ms | 68 ms |
