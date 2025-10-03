"""
Einfaches Tool zum Transformieren eines DICOM RTSTRUCT mittels REG-Datei (4x4-Matrix).

Funktionalität (minimal, fokussiert):
- Lies REG (.dcm), extrahiere 4x4-Transformationsmatrix.
- Wende die Matrix auf alle "ContourData"-Punkte im RTSTRUCT an (homogene Koordinaten).
- Optional: Übernehme die ReferencedFrameOfReferenceSequence aus einem Planungs-RTSTRUCT.
- Schreibe ein neues RTSTRUCT mit neuen Series-/SOP-UIDs (Study-UID bleibt unverändert).

Nutzung (Kommandozeile):

  python simple_transform_structureset.py \
    --rtstruct Pfad/zum/RTSTRUCT.dcm \
    --reg Pfad/zur/REG.dcm \
    --out Pfad/zur/Ausgabe_RTSTRUCT.dcm \
    [--ref-rtstruct Pfad/zum/Planungs_RTSTRUCT.dcm]

  Batch (CSV):

  CSV mit Spalten: rtstruct,reg,out[,ref_rtstruct]
  Delimiter: "," (Komma) oder ";" (Semikolon) werden automatisch erkannt.

    python simple_transform_structureset.py --batch pfad/zur/batch.csv

Wenn keine Argumente angegeben werden, öffnet das Tool einfache Dateidialoge.

Hinweise:
- Falls in der REG-Datei keine gültige 4x4-Matrix gefunden wird, wird die Einheitsmatrix verwendet
  und eine Warnung ausgegeben (damit bleibt das RTSTRUCT unverändert).
- Es werden keine weiteren Bearbeitungsschritte durchgeführt (kein Umbenennen, keine Margins,
  keine Volumenberechnungen etc.).

Voraussetzungen:
- Python 3.9+
- pydicom, numpy, (optional tkinter für Dateidialog)
"""

import os
import sys
import argparse
import warnings
from typing import Optional
import csv

import numpy as np
import pydicom
from pydicom.uid import generate_uid

try:
    import tkinter as tk
    from tkinter import filedialog
    TK_AVAILABLE = True
except Exception:
    TK_AVAILABLE = False


def extract_matrix_from_reg(reg_path: str) -> np.ndarray:
    """Extrahiert eine 4x4-Transformationsmatrix aus einer DICOM REG-Datei.

    Es werden mehrere übliche Stellen geprüft. Falls nichts Geeignetes gefunden wird,
    wird die 4x4-Einheitsmatrix zurückgegeben und eine Warnung ausgegeben.
    """
    ds = pydicom.dcmread(reg_path)

    def is_valid_matrix(mat: np.ndarray) -> bool:
        if mat.shape != (4, 4):
            return False
        flat = mat.flatten()
        # "echt" transformiert, nicht nur lauter 0/1 (Identität ist aber erlaubt, wenn so geliefert)
        return any((v not in (0.0, 1.0)) for v in flat)

    def try_get_matrix_from_reg_sequence(reg_seq) -> Optional[np.ndarray]:
        try:
            mreg = reg_seq.MatrixRegistrationSequence[0]
            mseq = mreg.MatrixSequence[0]
            vals = [float(v) for v in mseq.FrameOfReferenceTransformationMatrix]
            return np.array(vals, dtype=float).reshape(4, 4)
        except Exception:
            return None

    # 1) Standard: RegistrationSequence -> MatrixRegistrationSequence -> MatrixSequence
    try:
        for reg_seq in ds.RegistrationSequence:
            mat = try_get_matrix_from_reg_sequence(reg_seq)
            if mat is not None:
                return mat
    except Exception:
        pass

    # 2) Alternative Felder (nicht in allen Systemen vorhanden)
    try:
        for reg_seq in ds.RegistrationSequence:
            try:
                vals = [float(v) for v in reg_seq.RegistrationTransformationMatrix]
                mat = np.array(vals, dtype=float).reshape(4, 4)
                return mat
            except Exception:
                pass
            try:
                # Fallback: nur Translation als Vektor
                vec = [float(v) for v in reg_seq.Vector]
                tmat = np.eye(4, dtype=float)
                tmat[:3, 3] = vec[:3]
                return tmat
            except Exception:
                pass
    except Exception:
        pass

    warnings.warn(f"Keine gültige Matrix in REG-Datei gefunden: {reg_path}. Verwende Identität.")
    return np.eye(4, dtype=float)


def apply_transform_to_rtstruct(rtstruct_path: str,
                                reg_path: str,
                                out_path: str,
                                ref_rtstruct_path: Optional[str] = None) -> None:
    """Wendet die 4x4-Transformation (aus REG) auf alle Konturen im RTSTRUCT an und speichert ein neues RTSTRUCT.

    - Generiert neue SeriesInstanceUID und SOPInstanceUID (Study bleibt gleich).
    - Optional: kopiert die ReferencedFrameOfReferenceSequence aus ref_rtstruct_path.
    """
    # Lade Daten
    rt = pydicom.dcmread(rtstruct_path)
    mat = extract_matrix_from_reg(reg_path)

    # Optional FoR übernehmen
    if ref_rtstruct_path is not None:
        try:
            ref = pydicom.dcmread(ref_rtstruct_path)
            if hasattr(ref, 'ReferencedFrameOfReferenceSequence'):
                rt.ReferencedFrameOfReferenceSequence = ref.ReferencedFrameOfReferenceSequence
        except Exception as e:
            warnings.warn(f"Konnte Referenz-RTSTRUCT nicht lesen/verwenden: {e}")

    # Optional: Datum aus Ausgabedateinamen extrahieren (z.B. transformed_YYYYMMDD_...)
    # und StructureSetLabel setzen sowie ROI-Namen mit Datum versehen
    date_yyyymmdd = None
    try:
        base = os.path.basename(out_path)
        parts = base.split("_")
        if len(parts) > 1 and len(parts[1]) == 8 and parts[1].isdigit():
            date_yyyymmdd = parts[1]
    except Exception:
        pass

    if date_yyyymmdd:
        new_label = f"{date_yyyymmdd[2:]}_REGdiv"
        print(f"Setze StructureSetLabel auf: {new_label}")
        try:
            rt.StructureSetLabel = new_label
        except Exception:
            pass

    # Neue UIDs vergeben (Study bleibt, Series/SOP neu)
    rt.SeriesInstanceUID = generate_uid()
    rt.SOPInstanceUID = generate_uid()

    # Punkte transformieren
    changed_contours = 0
    if hasattr(rt, 'ROIContourSequence'):
        for roi_contour in rt.ROIContourSequence:
            if not hasattr(roi_contour, 'ContourSequence'):
                continue
            for contour in roi_contour.ContourSequence:
                data = getattr(contour, 'ContourData', None)
                if not data:
                    continue
                pts = np.array(data, dtype=float).reshape(-1, 3)
                out_pts = []
                for p in pts:
                    ph = np.append(p, 1.0)
                    tph = mat @ ph
                    w = tph[3] if tph.shape[0] == 4 else 1.0
                    if w != 0:
                        tp = tph[:3] / w
                    else:
                        tp = tph[:3]
                    out_pts.append(tp)
                contour.ContourData = [c for pt in out_pts for c in pt]
                changed_contours += 1

    # ROI-Namen aktualisieren: _CBCT -> _YYMMDD (falls Datum erkannt)
    if date_yyyymmdd and hasattr(rt, 'StructureSetROISequence'):
        short_date = date_yyyymmdd[2:]
        for roi_seq in rt.StructureSetROISequence:
            try:
                name = roi_seq.ROIName
            except Exception:
                continue
            if not isinstance(name, str):
                continue
            if name.endswith('_CBCT'):
                base_name = name.replace('_CBCT', '')
                new_name = f"{base_name}_{short_date}"
                print(f"  ROI-Name: {name} -> {new_name}")
                try:
                    roi_seq.ROIName = new_name
                except Exception:
                    pass

    # Speichern
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    rt.save_as(out_path)

    print("Transformation abgeschlossen.")
    print(f"  Eingabe RTSTRUCT : {rtstruct_path}")
    print(f"  REG-Datei        : {reg_path}")
    if ref_rtstruct_path:
        print(f"  Ref-RTSTRUCT     : {ref_rtstruct_path}")
    print(f"  Ausgabe          : {out_path}")
    print(f"  Transformierte Konturen: {changed_contours}")


def apply_batch(csv_path: str) -> None:
    """Führt mehrere Transformationen anhand einer CSV-Datei aus.

    Erwartete Spalten:
      - rtstruct (Pfad)
      - reg      (Pfad)
      - out      (Pfad)
      - ref_rtstruct (optional, Pfad)

    Delimiter wird automatisch zwischen Komma und Semikolon erkannt.
    """
    # Auto-Delimiter erkennen
    with open(csv_path, "r", newline="", encoding="utf-8") as f:
        sample = f.read(2048)
        f.seek(0)
        dialect = csv.Sniffer().sniff(sample, delimiters=",;")
        reader = csv.DictReader(f, dialect=dialect)

        required = {"rtstruct", "reg", "out"}
        missing_cols = required - set([c.strip() for c in reader.fieldnames or []])
        if missing_cols:
            raise ValueError(f"Fehlende Spalten in CSV: {', '.join(sorted(missing_cols))}")

        total = 0
        ok = 0
        errors = 0
        for row in reader:
            total += 1
            rtstruct = (row.get("rtstruct") or "").strip()
            reg = (row.get("reg") or "").strip()
            out = (row.get("out") or "").strip()
            ref = (row.get("ref_rtstruct") or "").strip() or None

            print("\n[Batch] Verarbeitung Eintrag:")
            print(f"  rtstruct     : {rtstruct}")
            print(f"  reg          : {reg}")
            print(f"  out          : {out}")
            if ref:
                print(f"  ref_rtstruct : {ref}")

            try:
                if not rtstruct or not reg or not out:
                    raise ValueError("rtstruct/reg/out dürfen nicht leer sein")
                apply_transform_to_rtstruct(rtstruct, reg, out, ref)
                ok += 1
            except Exception as e:
                errors += 1
                print(f"[Batch] Fehler: {e}")

        print("\nBatch-Zusammenfassung:")
        print(f"  Gesamt: {total}")
        print(f"  Erfolgreich: {ok}")
        print(f"  Fehler: {errors}")


def run_interactive():
    if not TK_AVAILABLE:
        print("Tkinter nicht verfügbar. Bitte Kommandozeilen-Argumente verwenden.")
        sys.exit(2)
    root = tk.Tk()
    root.withdraw()

    rtstruct_path = filedialog.askopenfilename(title="RTSTRUCT auswählen",
                                               filetypes=[("DICOM", "*.dcm"), ("Alle Dateien", "*.*")])
    if not rtstruct_path:
        print("Abgebrochen.")
        return

    reg_path = filedialog.askopenfilename(title="REG-Datei auswählen",
                                          filetypes=[("DICOM", "*.dcm"), ("Alle Dateien", "*.*")])
    if not reg_path:
        print("Abgebrochen.")
        return

    ref_rtstruct_path = filedialog.askopenfilename(title="(Optional) Referenz-RTSTRUCT auswählen",
                                                   filetypes=[("DICOM", "*.dcm"), ("Alle Dateien", "*.*")])
    out_path = filedialog.asksaveasfilename(title="Ausgabedatei wählen",
                                            defaultextension=".dcm",
                                            filetypes=[("DICOM", "*.dcm"), ("Alle Dateien", "*.*")])
    if not out_path:
        print("Abgebrochen.")
        return

    apply_transform_to_rtstruct(rtstruct_path, reg_path, out_path,
                                ref_rtstruct_path if ref_rtstruct_path else None)


def parse_args(argv=None):
    p = argparse.ArgumentParser(description="Transformiert ein DICOM RTSTRUCT mittels REG (4x4-Matrix).",
                                formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    # Einzel-Job Parameter
    p.add_argument("--rtstruct", type=str, help="Pfad zum RTSTRUCT (.dcm)")
    p.add_argument("--reg", type=str, help="Pfad zur REG-Datei (.dcm)")
    p.add_argument("--out", type=str, help="Pfad zur Ausgabedatei (.dcm)")
    p.add_argument("--ref-rtstruct", type=str, default=None,
                   help="Optional: Pfad zu Planungs-RTSTRUCT, dessen Frame of Reference übernommen wird")
    # Batch
    p.add_argument("--batch", type=str, default=None, help="Pfad zur CSV für Batch-Verarbeitung")
    p.add_argument("--interactive", action="store_true", help="Ohne Argumente: einfache Dateidialoge nutzen")
    return p.parse_args(argv)


if __name__ == "__main__":
    args = parse_args()

    if args.batch:
        apply_batch(args.batch)
    elif args.interactive or (not args.rtstruct and not args.reg and not args.out):
        run_interactive()
    else:
        missing = [name for name, val in (("--rtstruct", args.rtstruct), ("--reg", args.reg), ("--out", args.out)) if not val]
        if missing:
            print("Fehlende Argumente: " + ", ".join(missing))
            print("Tipp: Mit --interactive lassen sich Dateidialoge verwenden.")
            sys.exit(2)
        apply_transform_to_rtstruct(args.rtstruct, args.reg, args.out, args.ref_rtstruct)
