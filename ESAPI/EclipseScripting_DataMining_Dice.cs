using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMS.TPS;
using System.Windows.Media.Media3D;
using Microsoft.Win32; // For OpenFileDialog

namespace Example_Patients
{
    /// <summary>
    /// This class contains a method to calculate the Dice coefficient between two structures.
    /// (Note: The method uses triple-nested loops over a 3D grid, which can be computationally expensive for many structures.)
    /// </summary>
    public static class CalculateOverlap
    {
        public static double DiceCoefficient(Structure structure1, Structure structure2)
        {
            VVector p = new VVector();
            double volumeIntersection = 0;
            double volumeStructure1 = 0;
            double volumeStructure2 = 0;
            int intersectionCount = 0;
            int structure1Count = 0;
            int structure2Count = 0;
            double diceCoefficient = 0;

            Rect3D structure1Bounds = structure1.MeshGeometry.Bounds;
            Rect3D structure2Bounds = structure2.MeshGeometry.Bounds;
            Rect3D combinedRectBounds = Rect3D.Union(structure1Bounds, structure2Bounds);

            // Determine iteration limits based on the combined bounds.
            double startZ = Math.Floor(combinedRectBounds.Z - 1);
            double endZ = startZ + Math.Round(combinedRectBounds.SizeZ + 2);
            double startX = Math.Floor(combinedRectBounds.X - 1);
            double endX = startX + Math.Round(combinedRectBounds.SizeX + 2);
            double startY = Math.Floor(combinedRectBounds.Y - 1);
            double endY = startY + Math.Round(combinedRectBounds.SizeY + 2);

            if (structure1 != structure2)
            {
                // If one structure fully contains the other
                if (structure1Bounds.Contains(structure2Bounds))
                {
                    volumeIntersection = structure2.Volume;
                    volumeStructure1 = structure1.Volume;
                    volumeStructure2 = structure2.Volume;
                }
                else if (structure2Bounds.Contains(structure1Bounds))
                {
                    volumeIntersection = structure1.Volume;
                    volumeStructure1 = structure1.Volume;
                    volumeStructure2 = structure2.Volume;
                }
                else
                {
                    // Iterate over a grid within the combined region
                    for (double z = startZ; z < endZ; z += 1)
                    {
                        for (double y = startY; y < endY; y += 2)
                        {
                            for (double x = startX; x < endX; x += 2)
                            {
                                p.x = x;
                                p.y = y;
                                p.z = z;

                                if ((structure2Bounds.Contains(p.x, p.y, p.z)) &&
                                    (structure1.IsPointInsideSegment(p)) &&
                                    (structure2.IsPointInsideSegment(p)))
                                {
                                    intersectionCount++;
                                }
                                if (structure1.IsPointInsideSegment(p))
                                {
                                    structure1Count++;
                                }
                                if (structure2.IsPointInsideSegment(p))
                                {
                                    structure2Count++;
                                }
                                // Convert sample counts to volume (multipliers can be adjusted if needed)
                                volumeIntersection = (intersectionCount * 0.001 * 0.5);
                                volumeStructure1 = (structure1Count * 0.001 * 0.5);
                                volumeStructure2 = (structure2Count * 0.001 * 0.5);
                            }
                        }
                    }
                }
                diceCoefficient = Math.Round((2 * volumeIntersection) / (volumeStructure1 + volumeStructure2), 3);
                return diceCoefficient;
            }
            else
            {
                return 1; // identical structures: Dice = 1
            }
        }
    }

    class Program
    {
        // Helper method to replace invalid filename characters (if needed)
        private static string MakeFilenameValid(string s)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char ch in invalidChars)
            {
                s = s.Replace(ch, '_');
            }
            return s;
        }

        [STAThread] // Required for file dialogs
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Logging in...");
                using (Application app = Application.CreateApplication())
                {
                    Console.WriteLine("Running script...");
                    Execute(app);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Exception was thrown: " + exception.Message);
            }
            Console.WriteLine("Execution finished. Press any key to exit.");
            Console.ReadKey();
        }

        static void Execute(Application app)
        {
            // --- Select the PatientIDs file via OpenFileDialog ---
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select PatientIDs File";
            openFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            if (openFileDialog.ShowDialog() != true)
            {
                Console.WriteLine("No PatientIDs file selected. Exiting...");
                return;
            }
            string invivoPatientIDs = openFileDialog.FileName;
            Console.WriteLine("Selected PatientIDs file: " + invivoPatientIDs);
            string[] patientIDs = File.ReadAllLines(invivoPatientIDs);
            //string allPatientIds = string.Join(",", patientIDs);

            // --- Output path with timestamp (relative to execution directory) ---
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string outputDir = Path.Combine(baseDir, "Output", "DataMining");
            Directory.CreateDirectory(outputDir);
            string outputFile = Path.Combine(outputDir, "structureData_" + timestamp + ".csv");
            Console.WriteLine("Output will be saved to: " + outputFile);
            Console.WriteLine(patientIDs.Count());

            // Write the CSV header (columns: PatientID;StructureSetID;StructureID;Volume;CenterX;CenterY;CenterZ;DiceCoefficient;CenterDistance3D)
            string header = "PatientID;StructureSetID;StructureID;Volume;CenterX;CenterY;CenterZ;DiceCoefficient;CenterDistance3D\n";
            File.WriteAllText(outputFile, header);

            int patientCount = 0;
            // --- Iterate over all patients whose IDs are listed in the selected file ---
            //foreach (var patientSummary in app.PatientSummaries.Where(x => allPatientIds.Contains(x.Id)))

            foreach (string id in patientIDs)
            {
                try
                {
                    Patient patient = app.OpenPatientById(id);
                    Console.WriteLine("Processing patient: " + patient.Id);
                    //Patient patient = app.OpenPatient(patientSummary);
                    if (patient == null)
                    {
                        Console.WriteLine("  Cannot open patient " + patient.Id);
                        continue;
                    }
                    bool blase = false;
                    bool rectum = false;
                    bool prostate = false;
                    // Iterate over all structure sets of the patient.
                    foreach (StructureSet ss in patient.StructureSets)
                    {
                        // Only process structure sets whose ID contains "Blase", "Rektum" or "Prostata".
                        if (!(ss.Id.Contains("Blase") || ss.Id.Contains("Rektum") || ss.Id.Contains("Prostata")))
                            continue;

                        if (blase == true && ss.Id.Contains("Blase"))
                            continue;
                        if (rectum == true && ss.Id.Contains("Rektum"))
                            continue;
                        if (prostate == true && ss.Id.Contains("Prostata"))
                            continue;

                        if (ss.Id.Contains("Blase"))
                            blase = true;
                        if (ss.Id.Contains("Rektum"))
                            rectum = true;
                        if (ss.Id.Contains("Prostata"))
                            prostate = true;


                        Console.WriteLine("  Processing StructureSet: " + ss.Id);
                        // Use the reference structure as the first structure whose ID contains "_CT".
                        Structure referenceStructure = ss.Structures.FirstOrDefault(s => s.Id.Contains("_CT"));
                        if (referenceStructure == null)
                        {
                            Console.WriteLine("    No reference structure (_CT) found in StructureSet " + ss.Id);
                            continue;
                        }

                        // For each valid (non-empty) structure in the structure set:
                        foreach (Structure s in ss.Structures)
                        {
                            if (!s.HasSegment || s.IsEmpty)
                                continue;

                            double dice = 0;
                            double distance3D = 0;
                            if (s.Id == referenceStructure.Id)
                            {
                                dice = 1;
                                distance3D = 0;
                            }
                            else
                            {
                                dice = CalculateOverlap.DiceCoefficient(referenceStructure, s);
                                double deltaX = s.CenterPoint.x - referenceStructure.CenterPoint.x;
                                double deltaY = s.CenterPoint.y - referenceStructure.CenterPoint.y;
                                double deltaZ = s.CenterPoint.z - referenceStructure.CenterPoint.z;
                                distance3D = Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
                            }

                            // Create one CSV row:
                            // PatientID;StructureSetID;StructureID;Volume;CenterX;CenterY;CenterZ;DiceCoefficient;CenterDistance3D
                            string csvLine = string.Format("{0};{1};{2};{3:0.0000};{4:0.0000};{5:0.0000};{6:0.0000};{7};{8:0.0000}",
                                patient.Id.Replace(";", ""),
                                ss.Id.Replace(";", ""),
                                s.Id.Replace(";", ""),
                                s.Volume,
                                s.CenterPoint.x,
                                s.CenterPoint.y,
                                s.CenterPoint.z,
                                dice,
                                distance3D);

                            File.AppendAllText(outputFile, csvLine + "\n");
                            Console.WriteLine("    Processed structure: " + s.Id);
                        }
                    }
                    Console.WriteLine("Finished processing patient: " + patient.Id);
                    app.ClosePatient();
                    patientCount++;
                }
                catch(Exception e) {
                    Console.WriteLine(e.ToString());
                    app.ClosePatient();
                }
                
                
            }

            Console.WriteLine("Total patients processed: " + patientCount);
            Console.WriteLine("Results saved to: " + outputFile);
            Console.ReadKey();
        }
    }
}
