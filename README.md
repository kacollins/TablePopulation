# TablePopulation
Generates insert scripts.

The goal is to create the scripts to insert data into lookup tables for deployment.

The first column in the table must be the primary key.

Steps:
* Reads list of tables from the TablesToPopulate.supersecret file in the bin\Debug\Inputs folder.
    * Schema name and table name, separated by period
    * Identity flag, separated from schema/table name with comma
        * 1 = true, anything else = false
        * Defaults to true if not given
    * examples of tables with identity:
       * dbo.AccountType
       * dbo.ContactType,1
    * example of table without identity:
       * dbo.Department,0
* Gets the data in each table.
* Generates the script to insert each record if the ID does not already exist.
* Includes scripts to set identity_insert on and off if the table has an identity column.
* Outputs the scripts to one sql file per table in the bin\Debug\Outputs folder.
