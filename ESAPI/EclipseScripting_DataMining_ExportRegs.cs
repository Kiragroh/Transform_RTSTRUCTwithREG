using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMS.TPS;
using System.Security;
using System.Diagnostics;
using System.Windows.Forms;
using Application = VMS.TPS.Common.Model.API.Application;

namespace Example_Patients
{

    class Program
    {
        
        [STAThread] // Do not remove this attribute, otherwise the script will not work
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
                Console.WriteLine("Exception was thrown:" + exception.Message);
            }

            Console.WriteLine("Execution finished. Press any key to exit.");
            //Console.ReadKey();
        }
        static List<string> ReadIdsFromFile(string filePath)
        {
            List<string> ids = new List<string>();
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        ids.Add(line.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file: {ex.Message}");
            }
            return ids;
        }

        static void WriteUsedIdsToFile(string filePath, List<string> usedIds)
        {
            try
            {
                File.WriteAllLines(filePath, usedIds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to file: {ex.Message}");
            }
        }

        static bool IsIdAlreadyUsed(string newId, List<string> usedIds)
        {
            return usedIds.Contains(newId);
        }
        static string MakeFilenameValid(string s)
        {
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (char ch in invalidChars)
            {
                s = s.Replace(ch, '_');
            }
            return s;
        }

        static void Execute(Application app)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv";
            openFileDialog.Title = "Select a File";

            // Setzen Sie den Anfangsverzeichnis auf den Desktop
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // Oder setzen Sie den Anfangsverzeichnis auf den zuletzt verwendeten Ordner
            //openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Recent);

            List<string> FileIDs = new List<string>();
            string filePath = "";
            System.DateTime dt = System.DateTime.Now;
            string datetext = dt.ToString("yyyyMMddHHmmss");

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    filePath = openFileDialog.FileName;
                    FileIDs = ReadIdsFromFile(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred while reading the file: " + ex.Message);
                    return;
                }
            }

            int counter = 0;
            string msg = "";

            // TODO: Uncomment and configure for CSV logging
            //string filename = @"<LOG_FILE_PATH>"; // e.g., "C:\Temp\Logs\Log_SeriesDescriptions.csv"
            //string header = "Pat-ID;LastName; FirstName; CT-Date; CT-ID; seriesID; seriesComment\n";
            //if (File.Exists(filename))
            //    File.Delete(filename);
            //File.WriteAllText(filename, header);
            //string existingRows = File.ReadAllText(filename).ToString().Replace(";", "");
            string row = "";

            //foreach (var patientSummary in app.PatientSummaries.Where(x => AllPatientIds.Contains(x.Id)))
            foreach (string id in FileIDs)
            {
                
                // Stop after when a few records have been found
                if (counter > 10000000)
                    break;


                // Retrieve patient information
                Patient patient = app.OpenPatientById(id);
                try { 
                if (patient == null)
                    throw new ApplicationException("Cannot open patient " + id);

                int counter2 = 0;
                // Iterate through all 3D images...
                foreach (Study study in patient.Studies)
                {
                    foreach (Series s in study.Series.Where(x => x.Modality == SeriesModality.REG))
                    {
                            try
                            {

                                //sw.WriteLine("rem move all RTDose in study " + s.Id);
                                //string cmd = move + '"' + "0008,0052=SERIES" + '"' + " -k " + '"' + "0020,000E=" + s.UID + '"' + IP_PORT;
                                // sw.WriteLine(cmd);


                                DateTime dt2 = DateTime.Now;
                                string datetext2 = dt.ToString("yyyyMMddHHmmss");
                                //string temp = System.Environment.GetEnvironmentVariable("TEMP");
                                // TODO: Configure your command file output directory
                                string cmd = MakeFilenameValid(
                                string.Format(CMD_FILE_FMT, patient.LastName, patient.Id, datetext2));
                                cmd = Path.Combine(@"<COMMAND_FILE_OUTPUT_PATH>", cmd); // e.g., "C:\Temp\Commands\"

                                string move = "movescu -v -aet " + AET + " -aec " + AEC + " -aem " + AEM + " -S -k ";

                                StreamWriter sw = new StreamWriter(cmd, false, Encoding.Default);
                                // TODO: Configure your DCMTK binary path
                                string DCMTK_BIN_PATH = @"<DCMTK_BIN_PATH>"; // e.g., "C:\DCMTK\dcmtk-3.6.7\bin"

                                sw.WriteLine(@"@set PATH=%PATH%;" + DCMTK_BIN_PATH);

                                // write the command to move the 3D image data set

                                //sw.WriteLine("rem move 3D image " + plan.StructureSet.Image.Id);
                                string cmd2 = move + '"' + "0008,0052=SERIES" + '"' + " -k " + '"' + "0020,000E=" + s.UID + '"' + IP_PORT;
                                sw.WriteLine(cmd2);

                                sw.Flush();
                                sw.Close();

                                using (Process process = new Process())
                                {
                                    // this powershell command allows us to see the standard output and also log it.
                                    string command = string.Format(@"&'{0}'", cmd);
                                    // Configure the process using the StartInfo properties.
                                    process.StartInfo.FileName = "PowerShell.exe";


                                    process.StartInfo.Arguments = command;
                                    process.StartInfo.UseShellExecute = false;

                                    process.Start();
                                    process.WaitForExit();
                                    process.Close();
                                }
                                
                            }
                            catch { Console.Write("Error unten" + patient.Id); }
                        }


                }

                    if (Directory.Exists(ESAPIimportPath + patient.Id))
                    { 
                        string[] files = Directory.GetFiles(ESAPIimportPath + patient.Id);

                        foreach (string file in files)
                        {

                            string name = Path.GetFileName(file);
                            //MessageBox.Show(name);

                            // TODO: Configure your export destination path
                            string dest_file = Path.Combine(@"<EXPORT_DESTINATION_PATH>", name); // e.g., "C:\Temp\ExportedDICOM\"
                            msg += patient.Id + "_" + name + Environment.NewLine;
                            // Ensure that the target does not exist.  
                            if (File.Exists(dest_file))
                                File.Delete(dest_file);
                            // insert the code here to transform your source path name into your destination path name.
                            File.Move(file, dest_file);

                        }
                    }


                // TODO: Configure your ESAPI import path for cleanup
                DeleteEmptyFolders(@"<ESAPI_IMPORT_PATH>"); // e.g., "C:\Temp\ESAPIImport"
                // Close the current patient, otherwise we will not be able to open another patient
                 // Save Patient specific info to LogFile in same Folder
                string patientInfo = id + Environment.NewLine + msg;
                IEnumerable<string> stringCollection= patientInfo.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                File.AppendAllLines(Path.Combine(Path.GetDirectoryName(filePath), string.Format("Log_{0}.txt",datetext)), stringCollection);
                
                app.ClosePatient();
                
                counter++;
                }
                catch(Exception e)
                { 
                    Console.Write("Error: "+ patient.Id);
                    string patientInfo = id + Environment.NewLine + e.ToString();
                    IEnumerable<string> stringCollection = patientInfo.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    File.AppendAllLines(Path.Combine(Path.GetDirectoryName(filePath), string.Format("Log_{0}.txt", datetext)), stringCollection);
                }
            }
            //WriteUsedIdsToFile(usedIdsFilePath, usedIds);
            //System.Console.WriteLine("Results are saved in {0}", filename);
        }

                                                                                                                                                                                      //public string DCMTK_BIN_PATH= Directory.Exists(@"D:\DCMTK\dcmtk-3.6.7\bin")? @"D:\DCMTK\dcmtk-3.6.7\bin" : @"C:\DCMTK\dcmtk-3.6.7\bin"; // path to DCMTK binaries
        // TODO: Configure these constants for your environment
        public const string AET = @"DCMTK";                 // local AE title
        public const string AEC = @"VMSDBD1";               // AE title of VMS DB Daemon
        public const string AEM = @"VMSFD1";                 // AE title of VMS File Daemon
        public const string IP_PORT = @"<SERVER_IP> <PORT>"; // IP address of server hosting the DB Daemon, port daemon is listening to
        public const string CMD_FILE_FMT = @"move-DICOMRT-{0}({1})-{2}.cmd";
        public const string ESAPIimportPath = @"<ESAPI_IMPORT_PATH>"; // e.g., "C:\Temp\ESAPIImport\"
        //public int PlanCount = 0;
        static void DeleteEmptyFolders(string path)
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                DeleteEmptyFolders(directory);
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
        }
    }
    
}
