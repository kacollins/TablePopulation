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
            Console.WriteLine("Enter table name:");
            string tableName = Console.ReadLine();

            string queryText = $"SELECT * FROM {tableName}";
            DataTable dt = GetDataTable(queryText);
            Console.WriteLine(dt);

            Console.WriteLine("Press enter to exit:");
            Console.Read();
        }

        #region Methods

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
