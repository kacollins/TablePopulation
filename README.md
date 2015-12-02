# TablePopulation
Generates insert scripts.

The goal is to create the scripts to insert data into lookup tables for deployment.

Assumptions:
* The first column in the table is an integer.
* The first column in the table is the primary key.
* If the table has an identity column, it is the first column.

Steps:
* Read list of tables from the TablesToPopulate.supersecret file in the bin\Debug\Inputs folder.
    * Schema name
    * Table name
    * Identity flag
* Get the data in each table.
* Generate the script to insert each record if the ID does not already exist.
* Include scripts to set identity_insert on and off if the table has an identity column.
* Output the scripts to one sql file per table in the bin\Debug\Outputs folder.
