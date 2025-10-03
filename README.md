# RTSTRUCT Transform (REG -> RTSTRUCT)

Ein minimales, fokussiertes Tool zum Transformieren eines DICOM RTSTRUCT mittels REG-Datei (4x4-Matrix).

- Nur Kernfunktionalität: Matrix aus REG lesen, alle Konturen im RTSTRUCT transformieren.
- Optional: Frame of Reference (FoR) aus Planungs-RTSTRUCT übernehmen.
- Optionale Datumslogik über den Ausgabedateinamen (Label + Umbenennung von `_CBCT`-Strukturen).
- Batch-Verarbeitung via CSV.

## Wozu dient das optionale Referenz-RTSTRUCT (`--ref-rtstruct`)?

- Ein RTSTRUCT enthält eine `ReferencedFrameOfReferenceSequence` (FoR). Wenn Konturen aus einem CBCT-RTSTRUCT in den Planungs-FoR gebracht werden sollen, kann mit `--ref-rtstruct` das Planungs-RTSTRUCT angegeben werden.
- Das Tool kopiert dann dessen `ReferencedFrameOfReferenceSequence` in das transformierte RTSTRUCT. Ergebnis: gleicher FoR wie der Plan.
- Wichtig: Die Punkte werden ausschließlich per 4x4-Matrix transformiert. Das FoR-Update ändert nur die Referenzmetadaten.

## Datumslogik (optional)

- Falls der Ausgabedateiname dem Muster `transformed_YYYYMMDD_*.dcm` entspricht, wird:
  - `StructureSetLabel` auf `YYMMDD_REGdiv` gesetzt.
  - Jede ROI, deren Name auf `_CBCT` endet, zu `..._YYMMDD` umbenannt.

## Voraussetzungen

- Python 3.9+
- Pakete: `pydicom`, `numpy` (optional `tkinter` für Dateidialoge)

Installation der Abhängigkeiten:
```bash
pip install -r requirements.txt
```

## Nutzung

### 1) Einzel-Job (Kommandozeile)

```bash
python simple_transform_structureset.py \
  --rtstruct Pfad/zum/RTSTRUCT.dcm \
  --reg Pfad/zur/REG.dcm \
  --out Pfad/zur/Ausgabe_RTSTRUCT.dcm \
  [--ref-rtstruct Pfad/zum/Planungs_RTSTRUCT.dcm]
```

- `--rtstruct`: Eingabe-RTSTRUCT
- `--reg`: REG-DICOM mit 4x4-Transformationsmatrix
- `--out`: Ausgabedatei (RTSTRUCT). Optionales Datum via Dateiname, siehe Datumslogik.
- `--ref-rtstruct` (optional): Planungs-RTSTRUCT, dessen FoR übernommen wird

### 2) Interaktiv (Dateidialoge)

```bash
python simple_transform_structureset.py --interactive
```

- Wählen Sie nacheinander RTSTRUCT, REG, optional Referenz-RTSTRUCT, und Ausgabedatei per Dialog.

### 3) Batch-Verarbeitung (CSV)

- CSV-Datei mit Spalten: `rtstruct, reg, out[, ref_rtstruct]`
- Delimiter `,` (Komma) oder `;` (Semikolon) wird automatisch erkannt.

Beispiel-CSV (Komma):
```
rtstruct,reg,out,ref_rtstruct
C:/data/p1/rtstruct_cbct.dcm,C:/data/p1/reg_20240101.dcm,C:/data/p1/transformed_20240101_out.dcm,C:/data/p1/rtstruct_plan.dcm
C:/data/p2/rtstruct_cbct.dcm,C:/data/p2/reg_20240102.dcm,C:/data/p2/transformed_20240102_out.dcm,
```

Aufruf:
```bash
python simple_transform_structureset.py --batch C:/data/batch.csv
```

- Für jede Zeile wird ein Transformationslauf durchgeführt; `ref_rtstruct` ist optional.
- Am Ende erscheint eine Batch-Zusammenfassung.

## Technische Details

- Matrixextraktion aus REG:
  - Primär: `RegistrationSequence -> MatrixRegistrationSequence -> MatrixSequence -> FrameOfReferenceTransformationMatrix`.
  - Alternativ: `RegistrationTransformationMatrix` oder (Fallback) `Vector` für reine Translation.
  - Falls keine Matrix gefunden wird: Einheitsmatrix (Warnung) – Konturen bleiben unverändert.
- Transformation:
  - Punkte der `ContourData` als homogene Koordinaten `[x, y, z, 1]` mit 4x4-Matrix multiplizieren.
  - `SeriesInstanceUID` und `SOPInstanceUID` werden neu generiert (Study-UID bleibt).
- FoR-Update (optional):
  - Bei `--ref-rtstruct` wird dessen `ReferencedFrameOfReferenceSequence` in das Ergebnis kopiert.

## Dateien

- `simple_transform_structureset.py` — Hauptskript
- `requirements.txt` — Abhängigkeiten

## Lizenz

Bitte Lizenz ergänzen (z. B. MIT), falls das Repo öffentlich bereitgestellt wird.
