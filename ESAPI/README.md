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
- 
## 3) DICOM Registration Export (Console App)

**File:** `EclipseScripting_DataMining_ExportRegs.cs`

### Purpose
Console-style script that exports DICOM registration series using DCMTK and the Varian DICOM Daemon:

- Prompts you to select a text file containing patient IDs (one ID per line)
- Iterates through patients and finds all registration series (SeriesModality.REG)
- Uses DCMTK `movescu` command to export registration DICOM files
- Moves exported files from ESAPI import directory to configured destination
- Creates timestamped log files with export status

### Prerequisites
- **DCMTK**: Install DCMTK toolkit (e.g., dcmtk-3.6.5-win64-dynamic)
- **Varian DICOM Daemon**: Configure VMS DB Daemon and VMS File Daemon
- **Network Access**: Ensure connectivity to DICOM daemon server

### Configuration
Before running, update the following constants in the script:

```csharp
// DICOM Network Configuration
public const string AET = @"DCMTK";                 // Local AE title
public const string AEC = @"VMSDBD1";               // AE title of VMS DB Daemon  
public const string AEM = @"VMSFD1";                 // AE title of VMS File Daemon
public const string IP_PORT = @"<SERVER_IP> <PORT>"; // Server IP and port

// Path Configuration
public const string ESAPIimportPath = @"<ESAPI_IMPORT_PATH>";     // e.g., "C:\Temp\ESAPIImport\"
// Additional paths configured inline with TODO comments
```

### How it works
1. **Patient Selection**: Load patient IDs from selected text file
2. **Registration Detection**: Find all series with modality "REG" for each patient
3. **DICOM Export**: Generate DCMTK commands and execute via PowerShell
4. **File Management**: Move exported DICOM files to destination directory
5. **Logging**: Create detailed log files with export status and errors

### Output locations
- **Exported DICOM files**: `<EXPORT_DESTINATION_PATH>` (configured in script)
- **Command files**: `<COMMAND_FILE_OUTPUT_PATH>` (temporary DCMTK commands)
- **Log files**: Same directory as input file, named `Log_YYYYMMDD_HHMMSS.txt`

### References
For detailed information about DCMTK integration with Varian DICOM Daemon, see:
[Scripting the Varian DICOM DB Daemon with ESAPI](https://github.com/VarianAPIs/Varian-Code-Samples/wiki/Scripting-the-Varian-DICOM-DB-Daemon-with-ESAPI)

## Disclaimer

This code is provided as-is. Validate results independently before using in any clinical workflow.

