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

            foreach (Table table in result.Tables)
            {
                GenerateInsertScripts(table);
            }

            if (result.Errors.Any())
            {
                HandleTopLevelError(AppendLines(result.Errors), false);
            }
            else if (!result.Tables.Any())
            {
                HandleTopLevelError("No tables to compare!");
            }

            //TODO: Add silent mode
            //Console.WriteLine("Press enter to exit:");
            //Console.Read();
        }

        #region Methods

        private static void GenerateInsertScripts(Table table)
        {
            string queryText = $"SELECT * FROM {table.SchemaName}.{table.TableName}";
            DataTable dt = GetDataTable(queryText);

            List<DataRow> rows = dt.Rows.Cast<DataRow>().OrderBy(r => r.ItemArray[0]).ToList();
            List<Column> columns = GetColumnsForInsert(table);

            List<string> fileLines = new List<string>();

            if (columns.Any(c => c.IsIdentity))
            {
                fileLines.Add($"SET IDENTITY_INSERT {table.SchemaName}.{table.TableName} ON");
                fileLines.Add("");
            }

            foreach (DataRow row in rows)
            {
                fileLines.AddRange(GetInsertScript(row, columns, table));
            }

            if (columns.Any(c => c.IsIdentity))
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

        private static List<Column> GetColumnsForInsert(Table table)
        {
            string queryText = $@"SELECT c.name, c.system_type_id, c.is_identity{
                Environment.NewLine}FROM sys.columns c{
                Environment.NewLine}INNER JOIN sys.tables t{
                Environment.NewLine}ON c.object_id = t.object_id{
                Environment.NewLine}INNER JOIN sys.schemas s{
                Environment.NewLine}ON t.schema_id = s.schema_id{
                Environment.NewLine}WHERE s.name = '{table.SchemaName}'{
                Environment.NewLine}AND t.name = '{table.TableName}'{
                Environment.NewLine}AND is_computed = 0";

            DataTable dt = GetDataTable(queryText);
            List<Column> columns = dt.Rows.Cast<DataRow>().Select(dr => new Column(dr)).ToList();

            return columns;
        }

        private static List<string> GetInsertScript(DataRow row, List<Column> columns, Table table)
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

        private static string GetColumnValue(DataRow row, Column column)
        {
            string input = row[column.ColumnName].ToString();
            string output;

            if (column.SystemTypeID == (int)DataType.Bit)
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
            const string fileName = "TablesToPopulate.supersecret";

            List<string> lines = GetFileLines(fileName);
            const char period = '.';

            List<Table> tables = lines.Select(line => line.Split(period).ToList())
                                        .Where(parts => parts.Count == Enum.GetValues(typeof(TablePart)).Length)
                                        .Select(parts => new Table(parts))
                                        .ToList();

            List<string> errorMessages = GetFileErrors(lines, period, Enum.GetValues(typeof(TablePart)).Length, "schema/table format");

            if (errorMessages.Any())
            {
                //TODO: Write error messages to file
                Console.WriteLine("Error: Invalid schema/table format in TablesToPopulate file.");
            }

            TableFileResult result = new TableFileResult(tables, errorMessages);

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

            //TODO: Create subdirectory for each schema

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
                //TODO: Write error messages to file
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

        private static string CurrentDirectory => Directory.GetCurrentDirectory();  //bin\Debug

        private static string Tab => new string(' ', 4);

        #endregion

        #region Classes

        private class Table
        {
            public string SchemaName { get; }
            public string TableName { get; }

            public Table(List<string> parts)
            {
                SchemaName = parts[(int)TablePart.SchemaName].Trim();
                TableName = parts[(int)TablePart.TableName].Trim();
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

        private class Column
        {
            public string ColumnName { get; }
            public int SystemTypeID { get; }
            public bool IsIdentity { get; }

            public Column(DataRow dr)
            {
                ColumnName = dr["name"].ToString();
                SystemTypeID = int.Parse(dr["system_type_id"].ToString());
                IsIdentity = bool.Parse(dr["is_identity"].ToString());
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

        private enum DataType
        {
            Uniqueidentifier = 36,
            Date = 40,
            Integer = 56,
            Datetime = 61,
            Bit = 104,
            Dec = 106,
            Numeric = 108,
            Varchar = 167,
            Character = 175,
            Nvarchar = 231
        }

        #endregion

    }
}
