# RTSTRUCT Transform (REG -> RTSTRUCT)

A minimal, focused tool to transform a DICOM RTSTRUCT using a REG DICOM file (4x4 matrix).

- Core functionality only: read matrix from REG, transform all contours in the RTSTRUCT.
- Optional: copy Frame of Reference (FoR) from a planning RTSTRUCT.
- Optional date logic via output filename (label + renaming `_CBCT` structures).
- Batch processing via CSV.

## What is the optional reference RTSTRUCT (`--ref-rtstruct`) for?

- An RTSTRUCT contains a `ReferencedFrameOfReferenceSequence` (FoR). If contours from a CBCT RTSTRUCT should be placed into the planning FoR, you can supply the planning RTSTRUCT via `--ref-rtstruct`.
- The tool then copies its `ReferencedFrameOfReferenceSequence` into the transformed RTSTRUCT. Result: same FoR as the plan.
- Important: The points are transformed solely by the 4x4 matrix. The FoR update only changes reference metadata.

## Date logic (optional)

- If the output filename matches `transformed_YYYYMMDD_*.dcm`, then:
  - `StructureSetLabel` is set to `YYMMDD_REGdiv`.
  - Any ROI whose name ends with `_CBCT` is renamed to `..._YYMMDD`.

## Requirements

- Python 3.9+
- Packages: `pydicom`, `numpy` (optional `tkinter` for file dialogs)

Install dependencies:
```bash
pip install -r requirements.txt
```

## Usage

### 1) Single job (command line)

```bash
python simple_transform_structureset.py \
  --rtstruct path/to/RTSTRUCT.dcm \
  --reg path/to/REG.dcm \
  --out path/to/output_RTSTRUCT.dcm \
  [--ref-rtstruct path/to/planning_RTSTRUCT.dcm]
```

- `--rtstruct`: input RTSTRUCT
- `--reg`: REG DICOM with 4x4 transformation matrix
- `--out`: output file (RTSTRUCT). Optional date via filename, see Date logic.
- `--ref-rtstruct` (optional): planning RTSTRUCT whose FoR is copied

### 2) Interactive (file dialogs)

```bash
python simple_transform_structureset.py --interactive
```

- Select RTSTRUCT, REG, optional reference RTSTRUCT, and output file via dialog.

### 3) Batch processing (CSV)

- CSV file with columns: `rtstruct, reg, out[, ref_rtstruct]`
- Delimiter `,` (comma) or `;` (semicolon) is auto-detected.

Example CSV (comma):
```
rtstruct,reg,out,ref_rtstruct
C:/data/p1/rtstruct_cbct.dcm,C:/data/p1/reg_20240101.dcm,C:/data/p1/transformed_20240101_out.dcm,C:/data/p1/rtstruct_plan.dcm
C:/data/p2/rtstruct_cbct.dcm,C:/data/p2/reg_20240102.dcm,C:/data/p2/transformed_20240102_out.dcm,
```

Run:
```bash
python simple_transform_structureset.py --batch C:/data/batch.csv
```

- A transformation run is executed for each row; `ref_rtstruct` is optional.
- A batch summary is printed at the end.

## Technical details

- Matrix extraction from REG:
  - Primary: `RegistrationSequence -> MatrixRegistrationSequence -> MatrixSequence -> FrameOfReferenceTransformationMatrix`.
  - Alternative: `RegistrationTransformationMatrix` or (fallback) `Vector` for pure translation.
  - If no matrix is found: identity (warning) – contours remain unchanged.
- Transformation:
  - Multiply `ContourData` points, as homogeneous coordinates `[x, y, z, 1]`, by the 4x4 matrix.
  - `SeriesInstanceUID` and `SOPInstanceUID` are regenerated (Study UID remains).
- FoR update (optional):
  - With `--ref-rtstruct`, copy its `ReferencedFrameOfReferenceSequence` to the result.

## Files

- `simple_transform_structureset.py` — main script
- `requirements.txt` — dependencies

## License

Please add a license (e.g., MIT) if the repo is made public.
