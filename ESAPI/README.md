# ESAPI Structure Similarity Tools

This repository contains two C# scripts related to structure comparison / similarity metrics in Varian Eclipse (ESAPI).

## Contents

- `EclipseScripting_SingelFilePlugin_StructureSimilarityTool.cs`
- `EclipseScripting_DataMining_Dice.cs`

## 1) Structure Similarity Tool (Single-File Eclipse Plugin)

**File:** `EclipseScripting_SingelFilePlugin_StructureSimilarityTool.cs`

### Purpose
Interactive UI tool to compare two structures in the currently opened `StructureSet`:

- Volume of each structure
- Overlap volume and overlap percentage (relative to Structure 2)
- Dice coefficient (estimated via grid sampling)
- Shortest surface distance (mesh vertex-to-vertex)
- CenterPoint-to-CenterPoint distance

### Notes / limitations

- Sampling resolution is controlled via the constants `STEP_X_MM`, `STEP_Y_MM`, `STEP_Z_MM`.
- Shortest surface distance is computed via nested loops over mesh vertices and can be slow for high-resolution meshes.

## 2) Data Mining Dice Export (Console App)

**File:** `EclipseScripting_DataMining_Dice.cs`

### Purpose
Console-style script that:

- Prompts you to select a text file containing patient IDs (one ID per line)
- Iterates patients and selected structure sets
- Uses the first structure containing `_CT` as the reference structure
- Computes Dice coefficient against all other structures
- Exports results to a timestamped CSV file

### Output location
Results are written relative to the execution directory:

`<execution-directory>/Output/DataMining/structureData_YYYYMMDD_HHMMSS.csv`

### CSV columns

`PatientID;StructureSetID;StructureID;Volume;CenterX;CenterY;CenterZ;DiceCoefficient;CenterDistance3D`

### Notes / limitations

- Dice is estimated via sampling on a 3D grid and can be computationally expensive.
- The script currently filters `StructureSet.Id` by German keywords: `Blase`, `Rektum`, `Prostata`. Adjust as needed for your naming conventions.

## Requirements

- Varian Eclipse ESAPI environment / assemblies available at build/runtime.
- Appropriate permissions to open patients and access structure sets.

## Disclaimer

This code is provided as-is. Validate results independently before using in any clinical workflow.
