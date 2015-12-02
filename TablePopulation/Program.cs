using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TablePopulation
{
    class Program
    {
        static void Main(string[] args)
        {
            TableFileResult result = GetTablesToPopulate();

            if (result.Tables.Any())
            {
                foreach (Table table in result.Tables)
                {
                    GenerateInsertScripts(table);
                }
            }

            if (result.Errors.Any())
            {
                HandleTopLevelError(AppendLines(result.Errors), false);
            }
            else if (!result.Tables.Any())
            {
                HandleTopLevelError("No tables to compare!");
            }


            Console.WriteLine("Press enter to exit:");
            Console.Read();
        }

        private static void GenerateInsertScripts(Table table)
        {
            string queryText = $"SELECT * FROM {table.SchemaName}.{table.TableName}";
            DataTable dt = GetDataTable(queryText);
            Console.WriteLine($"{table.SchemaName}.{table.TableName} has {dt.Rows.Count} rows.");

            //TODO: Validate that ID is unique integer

            List<string> fileLines = (from DataRow row in dt.Rows select $"--TODO: Insert record for ID = {GetID(row)}").ToList();

            string fileName = $"{table.SchemaName}_{table.TableName}";
            string fileContents = AppendLines(fileLines);

            if (string.IsNullOrWhiteSpace(fileContents))
            {
                fileContents = "--No records to insert";
            }

            WriteToFile(fileName, fileContents);
        }

        private static int GetID(DataRow dr)
        {
            return int.Parse(dr.ItemArray[0].ToString());
        }

        #region Methods

        private static TableFileResult GetTablesToPopulate()
        {
            string fileName = "TablesToPopulate.supersecret";

            List<string> lines = GetFileLines(fileName);
            const char separator = '.';

            List<Table> tablesToPopulate = lines.Where(line => line.Split(separator).Length == Enum.GetValues(typeof(TablePart)).Length)
                                                .Select(validLine => validLine.Split(separator))
                                                .Select(parts => new Table(parts[(int)TablePart.SchemaName],
                                                                            parts[(int)TablePart.TableName]))
                                                .ToList();

            List<string> errorMessages = GetFileErrors(lines, separator, Enum.GetValues(typeof(TablePart)).Length, "schema/table format");

            if (errorMessages.Any())
            {
                Console.WriteLine($"Error: Invalid schema/table format in TablesToPopulate file.");
            }

            TableFileResult result = new TableFileResult(tablesToPopulate, errorMessages);

            return result;
        }

        private static List<string> GetFileLines(string fileName)
        {
            List<string> fileLines = new List<string>();

            const char backSlash = '\\';
            DirectoryInfo directoryInfo = new DirectoryInfo($"{CurrentDirectory}{backSlash}{Folder.Inputs}");

            if (directoryInfo.Exists)
            {
                FileInfo file = directoryInfo.GetFiles(fileName).FirstOrDefault();

                if (file == null)
                {
                    Console.WriteLine($"File does not exist: {directoryInfo.FullName}{backSlash}{fileName}");
                }
                else
                {
                    fileLines = File.ReadAllLines(file.FullName)
                                            .Where(line => !string.IsNullOrWhiteSpace(line)
                                                            && !line.StartsWith("--")
                                                            && !line.StartsWith("//")
                                                            && !line.StartsWith("'"))
                                            .ToList();
                }
            }
            else
            {
                Console.WriteLine($"Directory does not exist: {directoryInfo.FullName}");
            }

            return fileLines;
        }

        private static List<string> GetFileErrors(List<string> fileLines, char separator, int length, string description)
        {
            List<string> errorMessages = fileLines.Where(line => line.Split(separator).Length != length)
                                                    .Select(invalidLine => $"Invalid {description}: {invalidLine}")
                                                    .ToList();

            return errorMessages;
        }

        private static string AppendLines(IEnumerable<string> input)
        {
            return input.Aggregate(new StringBuilder(), (current, next) => current.AppendLine(next)).ToString();
        }

        private static void HandleTopLevelError(string errorMessage, bool writeToConsole = true)
        {
            if (writeToConsole)
            {
                Console.WriteLine(errorMessage);
            }

            WriteToFile("Error", errorMessage);
        }

        private static void WriteToFile(string fileName, string fileContents)
        {
            const char backSlash = '\\';
            string directory = $"{CurrentDirectory}{backSlash}{Folder.Outputs}";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string filePath = $"{directory}{backSlash}{fileName}.sql";

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using (StreamWriter sw = File.CreateText(filePath))
            {
                sw.Write(fileContents);
            }

            Console.WriteLine($"Wrote file to {filePath}");
        }

        private static DataTable GetDataTable(string queryText)
        {
            SqlConnection conn = new SqlConnection
            {
                ConnectionString = ConfigurationManager.ConnectionStrings["SQLDBConnection"].ToString()
            };

            SqlDataAdapter sda = new SqlDataAdapter(queryText, conn);
            DataTable dt = new DataTable();

            try
            {
                conn.Open();
                sda.Fill(dt);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        #endregion

        #region Properties

        //In LINQPad: private static string CurrentDirectory => Path.GetDirectoryName(Util.CurrentQueryPath);
        private static string CurrentDirectory => Directory.GetCurrentDirectory();  //bin\Debug

        #endregion

        #region Classes

        private class Table
        {
            public string SchemaName { get; }
            public string TableName { get; }

            public Table(string schemaName, string tableName)
            {
                SchemaName = schemaName;
                TableName = tableName;
            }
        }

        private class TableFileResult
        {
            public List<Table> Tables { get; }
            public List<string> Errors { get; }

            public TableFileResult(List<Table> tables, List<string> errors)
            {
                Tables = tables;
                Errors = errors;
            }
        }

        #endregion

        #region Enums

        private enum TablePart
        {
            SchemaName,
            TableName
        }

        private enum Folder
        {
            Inputs,
            Outputs
        }

        #endregion

    }
}
