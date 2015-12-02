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
        
    }
}
