using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

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

        #region Methods

        private static void GenerateInsertScripts(Table table)
        {
            string queryText = $"SELECT * FROM {table.SchemaName}.{table.TableName}";
            DataTable dt = GetDataTable(queryText);

            List<DataRow> rows = dt.Rows.Cast<DataRow>().OrderBy(r => r.ItemArray[0]).ToList();
            List<DataColumn> columns = dt.Columns.Cast<DataColumn>().ToList();

            List<string> fileLines = new List<string>();

            if (table.HasIdentity)
            {
                fileLines.Add($"SET IDENTITY_INSERT {table.SchemaName}.{table.TableName} ON");
                fileLines.Add("");
            }

            foreach (DataRow row in rows)
            {
                fileLines.AddRange(GetInsertScript(row, columns, table));
            }

            if (table.HasIdentity)
            {
                fileLines.Add($"SET IDENTITY_INSERT {table.SchemaName}.{table.TableName} OFF");
                fileLines.Add("");
            }

            string fileName = $"{table.SchemaName}_{table.TableName}";
            string fileContents = AppendLines(fileLines);

            if (string.IsNullOrWhiteSpace(fileContents))
            {
                fileContents = "--No records to insert";
            }

            WriteToFile(fileName, fileContents);
        }

        private static List<string> GetInsertScript(DataRow row, List<DataColumn> columns, Table table)
        {
            List<string> scriptLines = new List<string>();

            scriptLines.Add("IF NOT EXISTS (SELECT *");
            scriptLines.Add($"{Tab}{Tab}{Tab}{Tab}FROM {table.SchemaName}.{table.TableName}");
            scriptLines.Add($"{Tab}{Tab}{Tab}{Tab}WHERE {columns.First().ColumnName} = {row[0]})");

            scriptLines.Add($"INSERT INTO {table.SchemaName}.{table.TableName}");
            scriptLines.Add("(");
            scriptLines.Add($"{Tab}{columns.First().ColumnName}");
            scriptLines.AddRange(columns.Skip(1).Select(column => $"{Tab}, {column.ColumnName}"));
            scriptLines.Add(")");
            scriptLines.Add("SELECT");

            scriptLines.Add($"{Tab}{columns.First().ColumnName} = {row[0]}");
            scriptLines.AddRange(columns.Skip(1).Select(column => $"{Tab}, {column.ColumnName} = '{GetColumnValue(row, column)}'"));
            scriptLines.Add("");

            return scriptLines;
        }

        private static string GetColumnValue(DataRow row, DataColumn column)
        {
            string input = row[column].ToString();
            string output;

            if (column.DataType == typeof(bool))
            {
                output = Convert.ToInt32(bool.Parse(input)).ToString();
            }
            else
            {
                output = input.Replace("'", "''");
            }

            return output;
        }

        private static TableFileResult GetTablesToPopulate()
        {
            string fileName = "TablesToPopulate.supersecret";

            List<string> lines = GetFileLines(fileName);
            const char comma = ',';

            var fileLines = lines.Select(line => line.Split(comma))
                                                .Select(parts => new
                                                {
                                                    TableName = parts[0],
                                                    Identity = parts.Length == 1 //assume identity if not specified
                                                            || (parts.Length == 2 && parts[1] == Convert.ToInt32(true).ToString())
                                                })
                                                .ToList();

            const char period = '.';

            List<Table> tablesToPopulate = fileLines.Select(line => new { TableParts = line.TableName.Split(period), line.Identity })
                                                                    .Where(x => x.TableParts.Length == Enum.GetValues(typeof(TablePart)).Length)
                                                                    .Select(x => new Table(x.TableParts[(int)TablePart.SchemaName],
                                                                                            x.TableParts[(int)TablePart.TableName],
                                                                                            x.Identity))
                                                                    .ToList();

            List<string> errorMessages = GetFileErrors(lines, period, Enum.GetValues(typeof(TablePart)).Length, "schema/table format");

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

        private static string Tab => new string(' ', 4);

        #endregion

        #region Classes

        private class Table
        {
            public string SchemaName { get; }
            public string TableName { get; }
            public bool HasIdentity { get; }

            public Table(string schemaName, string tableName, bool hasIdentity)
            {
                SchemaName = schemaName;
                TableName = tableName;
                HasIdentity = hasIdentity;
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
